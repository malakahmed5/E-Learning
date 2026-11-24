using ELearning.Core.Contracts;
using ELearning.Core.Entities;
using ELearning.Core.Entities.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using System.Text.Json;

namespace ELearning.Infrastructure.Data
{
    // Writes the activity log automatically.
    //
    // Whenever an assistant saves changes to the teacher's database, this class adds one
    // "audit log" row per changed record: who, what, which record, and the changed values.
    //
    // It runs inside the database layer on purpose. A service cannot forget to log,
    // because logging does not depend on the service calling anything.
    //
    // How it works (two steps, because a new record gets its id only after it is saved):
    //   1. Before saving: remember which records are about to change, and open a transaction.
    //   2. After saving:  write the log rows (now the ids are known) and commit the transaction.
    // The transaction makes sure the change and its log row are saved together or not at all.
    public class AuditInterceptor : SaveChangesInterceptor
    {
        // These change on every save and tell the teacher nothing useful.
        private static readonly string[] IgnoredProperties = ["CreatedAt", "UpdatedAt"];

        private readonly IAuditActor _actor;
        private List<PendingChange> _pendingChanges = [];
        private IDbContextTransaction? _ownTransaction;
        private bool _isSavingLogRows;

        public AuditInterceptor(IAuditActor actor)
        {
            _actor = actor;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var context = eventData.Context;
            if (context is null || _isSavingLogRows || !_actor.ShouldAudit)
                return await base.SavingChangesAsync(eventData, result, cancellationToken);

            _pendingChanges = CollectChanges(context);

            // Open a transaction only when the caller has not opened one already.
            if (_pendingChanges.Count > 0 && context.Database.CurrentTransaction is null)
                _ownTransaction = await context.Database.BeginTransactionAsync(cancellationToken);

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        public override async ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            var context = eventData.Context;
            if (context is not null && !_isSavingLogRows && _pendingChanges.Count > 0)
            {
                foreach (var change in _pendingChanges)
                    context.Add(change.ToAuditLog(_actor.UserId!.Value));

                _pendingChanges = [];

                // Saving the log rows calls this interceptor again; the flag stops an endless loop.
                _isSavingLogRows = true;
                try
                {
                    await context.SaveChangesAsync(cancellationToken);
                }
                finally
                {
                    _isSavingLogRows = false;
                }

                if (_ownTransaction is not null)
                {
                    await _ownTransaction.CommitAsync(cancellationToken);
                    await _ownTransaction.DisposeAsync();
                    _ownTransaction = null;
                }
            }

            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        public override async Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            // The save failed: nothing must be kept, neither the change nor a log row.
            _pendingChanges = [];
            if (_ownTransaction is not null)
            {
                await _ownTransaction.RollbackAsync(cancellationToken);
                await _ownTransaction.DisposeAsync();
                _ownTransaction = null;
            }

            await base.SaveChangesFailedAsync(eventData, cancellationToken);
        }

        // The steps above exist only for the async save. Refuse the other kind for audited users,
        // so a change can never slip past the activity log.
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            if (_actor.ShouldAudit && !_isSavingLogRows)
                throw new InvalidOperationException("Use SaveChangesAsync so the activity log can be written.");

            return base.SavingChanges(eventData, result);
        }

        private static List<PendingChange> CollectChanges(DbContext context)
        {
            var changes = new List<PendingChange>();

            foreach (var entry in context.ChangeTracker.Entries())
            {
                // Log rows themselves are not logged.
                if (entry.Entity is AuditLog)
                    continue;

                var isChange = entry.State == EntityState.Added
                               || entry.State == EntityState.Modified
                               || entry.State == EntityState.Deleted;
                if (!isChange)
                    continue;

                var details = DescribeChange(entry);

                // A "modified" record where only the ignored properties changed is not worth a log row.
                if (entry.State == EntityState.Modified && details.Count == 0)
                    continue;

                changes.Add(new PendingChange(entry, entry.State, details));
            }
            return changes;
        }

        // New record: every value. Changed record: old and new value of what changed. Deleted record: its last values.
        private static Dictionary<string, object?> DescribeChange(EntityEntry entry)
        {
            var details = new Dictionary<string, object?>();

            foreach (var property in entry.Properties)
            {
                var name = property.Metadata.Name;
                if (IgnoredProperties.Contains(name) || property.Metadata.IsPrimaryKey())
                    continue;

                if (entry.State == EntityState.Added)
                    details[name] = property.CurrentValue;
                else if (entry.State == EntityState.Deleted)
                    details[name] = property.OriginalValue;
                else if (property.IsModified && !Equals(property.OriginalValue, property.CurrentValue))
                    details[name] = new { old = property.OriginalValue, @new = property.CurrentValue };
            }
            return details;
        }

        private sealed record PendingChange(EntityEntry Entry, EntityState State, Dictionary<string, object?> Details)
        {
            public AuditLog ToAuditLog(int userId)
            {
                // Read the id now: for a new record it exists only after the save.
                var keyValue = Entry.Properties.First(p => p.Metadata.IsPrimaryKey()).CurrentValue;

                return new AuditLog
                {
                    UserId = userId,
                    Action = State switch
                    {
                        EntityState.Added => "Created",
                        EntityState.Deleted => "Deleted",
                        _ => "Updated",
                    },
                    EntityName = Entry.Metadata.ClrType.Name,
                    EntityId = keyValue?.ToString() ?? string.Empty,
                    Details = JsonSerializer.Serialize(Details),
                };
            }
        }
    }
}
