using ELearning.Core.Entities.Auditing;
using ELearning.Core.Entities.Groups;
using ELearning.Core.Entities.Identity;
using ELearning.Core.Entities.Students;
using ELearning.Core.Entities.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Core.Contracts
{
    // The current teacher's own database. What the services see: DbSets + SaveChanges.
    // EF Core's DbContext already is the unit of work, and each DbSet is the repository.
    public interface IELearningDbContext : IAsyncDisposable
    {
        DbSet<User> Users { get; }

        DbSet<Center> Centers { get; }
        DbSet<Group> Groups { get; }
        DbSet<GroupSchedule> GroupSchedules { get; }

        DbSet<Student> Students { get; }
        DbSet<SubscriptionPeriod> SubscriptionPeriods { get; }

        DbSet<AuditLog> AuditLogs { get; }

        DbSet<TEntity> Set<TEntity>() where TEntity : class;
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
