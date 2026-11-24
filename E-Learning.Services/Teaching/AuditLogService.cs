using ELearning.Core.Contracts;
using ELearning.Services.Abstraction;
using ELearning.Shared.DTOs;
using ELearning.Shared.DTOs.Teaching;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Services.Teaching
{
    // Teacher only: reads the activity log (what the assistants did).
    // The log rows are written automatically by AuditInterceptor in the Infrastructure project.
    public class AuditLogService : IAuditLogService
    {
        private readonly IELearningDbContext _database;

        public AuditLogService(IELearningDbContext database)
        {
            _database = database;
        }

        public async Task<PaginatedResult<AuditLogDto>> ListAsync(AuditLogQuery query)
        {
            var logs = _database.AuditLogs.AsNoTracking();

            if (query.UserId is not null)
                logs = logs.Where(l => l.UserId == query.UserId);

            if (query.From is not null)
            {
                var from = AsUtc(query.From.Value);
                logs = logs.Where(l => l.CreatedAt >= from);
            }

            if (query.To is not null)
            {
                var to = AsUtc(query.To.Value);
                logs = logs.Where(l => l.CreatedAt <= to);
            }

            var totalCount = await logs.CountAsync();

            // Newest first: the teacher usually wants to see what just happened.
            var items = await logs
                .OrderByDescending(l => l.CreatedAt)
                .ThenByDescending(l => l.Id)
                .Skip((query.PageIndex - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(l => new AuditLogDto
                {
                    Id = l.Id,
                    UserId = l.UserId,
                    UserName = l.User.FullName,
                    Action = l.Action,
                    EntityName = l.EntityName,
                    EntityId = l.EntityId,
                    Details = l.Details,
                    CreatedAt = l.CreatedAt,
                })
                .ToListAsync();

            return new PaginatedResult<AuditLogDto>(totalCount, query.PageSize, query.PageIndex, items);
        }

        // PostgreSQL stores times in UTC and refuses a date that does not say which time zone it is in.
        private static DateTime AsUtc(DateTime value)
        {
            return value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
    }
}
