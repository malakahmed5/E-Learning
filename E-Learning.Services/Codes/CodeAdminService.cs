using ELearning.Services.Abstraction;
using ELearning.Services.Security;
using ELearning.Shared.CommonResponses;
using ELearning.Shared.DTOs;
using ELearning.Shared.DTOs.Codes;
using ELearningHub.Core.Contracts;
using ELearningHub.Core.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ELearning.Services.Codes
{
    // Platform owner only: generates codes, lists them, shows numbers, and cancels unused codes.
    // Everything here is in the hub database, because codes do not belong to a teacher.
    public class CodeAdminService : ICodeAdminService
    {
        // How many days each kind of code gives.
        private const int FreeWeekDays = 7;
        private const int SubscriptionDays = 30;

        private readonly IHubDbContext _hub;
        private readonly ICurrentUser _currentUser;
        private readonly TimeProvider _clock;

        public CodeAdminService(IHubDbContext hub, ICurrentUser currentUser, TimeProvider clock)
        {
            _hub = hub;
            _currentUser = currentUser;
            _clock = clock;
        }

        public async Task<Result<GeneratedBatchDto>> GenerateAsync(GenerateCodesRequest request)
        {
            var now = UtcNow();

            DateTime? redeemBefore = null;
            if (request.RedeemBefore is not null)
            {
                redeemBefore = AsUtc(request.RedeemBefore.Value);
                if (redeemBefore <= now)
                    return CodeErrors.RedeemBeforeInPast;
            }

            var type = request.Type == CodeKind.FreeWeek ? CodeType.FreeWeek : CodeType.Subscription30;
            var values = await CreateUniqueValuesAsync(request.Count);

            var batch = new CodeBatch
            {
                Type = type,
                Count = request.Count,
                DurationDays = type == CodeType.FreeWeek ? FreeWeekDays : SubscriptionDays,
                RedeemBefore = redeemBefore,
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                CreatedByAccountId = _currentUser.AccountId,
                Codes = values.Select(value => new Code { Value = value }).ToList(),
            };
            _hub.CodeBatches.Add(batch);
            await _hub.SaveChangesAsync();

            // The log says who generated how many codes of which kind. It never contains the codes themselves.
            AddAuditLog("CodesGenerated", nameof(CodeBatch), batch.Id, new { type = type.ToString(), count = request.Count, redeemBefore });
            await _hub.SaveChangesAsync();

            return new GeneratedBatchDto
            {
                BatchId = batch.Id,
                Type = request.Type,
                DurationDays = batch.DurationDays,
                RedeemBefore = batch.RedeemBefore,
                Codes = values.Select(SecureCode.Format).ToList(),
            };
        }

        public async Task<PaginatedResult<CodeDto>> ListAsync(CodeQuery query)
        {
            var now = UtcNow();
            var codes = _hub.Codes.AsNoTracking();

            if (query.BatchId is not null)
                codes = codes.Where(c => c.BatchId == query.BatchId);

            if (query.Type is not null)
            {
                var type = query.Type == CodeKind.FreeWeek ? CodeType.FreeWeek : CodeType.Subscription30;
                codes = codes.Where(c => c.Batch.Type == type);
            }

            // "Expired" is not stored. It is worked out from the batch's last redeem date.
            if (query.Status == CodeState.Redeemed)
                codes = codes.Where(c => c.Status == CodeStatus.Used);
            else if (query.Status == CodeState.Revoked)
                codes = codes.Where(c => c.Status == CodeStatus.Voided);
            else if (query.Status == CodeState.Expired)
                codes = codes.Where(c => c.Status == CodeStatus.Available && c.Batch.RedeemBefore != null && c.Batch.RedeemBefore <= now);
            else if (query.Status == CodeState.Available)
                codes = codes.Where(c => c.Status == CodeStatus.Available && (c.Batch.RedeemBefore == null || c.Batch.RedeemBefore > now));

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                // The owner may paste a code with dashes or type part of a phone number.
                var codePart = SecureCode.Normalize(query.Search);
                var phonePart = query.Search.Trim();
                codes = codes.Where(c =>
                    c.Value.Contains(codePart) || (c.UsedByAccount != null && c.UsedByAccount.PhoneNumber.Contains(phonePart)));
            }

            var totalCount = await codes.CountAsync();

            var page = await codes
                .OrderByDescending(c => c.Id)
                .Skip((query.PageIndex - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(c => new
                {
                    Code = c,
                    c.Batch,
                    RedeemedByName = c.UsedByAccount == null ? null : c.UsedByAccount.FullName,
                    RedeemedByPhone = c.UsedByAccount == null ? null : c.UsedByAccount.PhoneNumber,
                    RedeemedAtTeacher = c.UsedInTenant == null ? null : c.UsedInTenant.DisplayName,
                })
                .ToListAsync();

            var items = new List<CodeDto>();
            foreach (var row in page)
            {
                items.Add(new CodeDto
                {
                    Id = row.Code.Id,
                    Code = SecureCode.Format(row.Code.Value),
                    Type = row.Batch.Type == CodeType.FreeWeek ? CodeKind.FreeWeek : CodeKind.Subscription30,
                    DurationDays = row.Batch.DurationDays,
                    Status = GetState(row.Code.Status, row.Batch.RedeemBefore, now),
                    BatchId = row.Batch.Id,
                    BatchNote = row.Batch.Note,
                    CreatedAt = row.Code.CreatedAt,
                    RedeemBefore = row.Batch.RedeemBefore,
                    RedeemedByName = row.RedeemedByName,
                    RedeemedByPhone = row.RedeemedByPhone,
                    RedeemedAtTeacher = row.RedeemedAtTeacher,
                    RedeemedAt = row.Code.UsedAt,
                });
            }

            return new PaginatedResult<CodeDto>(totalCount, query.PageSize, query.PageIndex, items);
        }

        public async Task<List<CodeStatsDto>> GetStatsAsync()
        {
            var now = UtcNow();

            // One query that counts codes per type, stored status, and "is the redeem date over".
            var groups = await _hub.Codes
                .GroupBy(c => new
                {
                    c.Batch.Type,
                    c.Status,
                    IsPastRedeemDate = c.Batch.RedeemBefore != null && c.Batch.RedeemBefore <= now,
                })
                .Select(counted => new { counted.Key.Type, counted.Key.Status, counted.Key.IsPastRedeemDate, Count = counted.Count() })
                .ToListAsync();

            var result = new List<CodeStatsDto>();
            foreach (var kind in new[] { CodeKind.FreeWeek, CodeKind.Subscription30 })
            {
                var type = kind == CodeKind.FreeWeek ? CodeType.FreeWeek : CodeType.Subscription30;
                var ofThisType = groups.Where(g => g.Type == type).ToList();

                result.Add(new CodeStatsDto
                {
                    Type = kind,
                    Total = ofThisType.Sum(g => g.Count),
                    Redeemed = ofThisType.Where(g => g.Status == CodeStatus.Used).Sum(g => g.Count),
                    Revoked = ofThisType.Where(g => g.Status == CodeStatus.Voided).Sum(g => g.Count),
                    Expired = ofThisType.Where(g => g.Status == CodeStatus.Available && g.IsPastRedeemDate).Sum(g => g.Count),
                    Available = ofThisType.Where(g => g.Status == CodeStatus.Available && !g.IsPastRedeemDate).Sum(g => g.Count),
                });
            }
            return result;
        }

        public async Task<Result> RevokeAsync(int codeId)
        {
            var exists = await _hub.Codes.AnyAsync(c => c.Id == codeId);
            if (!exists)
                return CodeErrors.NotFound;

            // One database command that only works while the code is still unused.
            // If a student redeems the code at the same moment, only one of the two actions wins.
            var revokedRows = await _hub.Codes
                .Where(c => c.Id == codeId && c.Status == CodeStatus.Available)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(c => c.Status, CodeStatus.Voided)
                    .SetProperty(c => c.UpdatedAt, (DateTime?)UtcNow()));

            if (revokedRows == 0)
            {
                var status = await _hub.Codes.Where(c => c.Id == codeId).Select(c => c.Status).FirstAsync();
                return status == CodeStatus.Used ? CodeErrors.AlreadyUsed : CodeErrors.Revoked;
            }

            AddAuditLog("CodeRevoked", nameof(Code), codeId, details: null);
            await _hub.SaveChangesAsync();
            return Result.Ok();
        }

        public async Task<Result<List<string>>> ExportBatchAsync(int batchId)
        {
            if (!await _hub.CodeBatches.AnyAsync(b => b.Id == batchId))
                return CodeErrors.BatchNotFound;

            var values = await _hub.Codes
                .Where(c => c.BatchId == batchId)
                .OrderBy(c => c.Id)
                .Select(c => c.Value)
                .ToListAsync();

            AddAuditLog("CodesExported", nameof(CodeBatch), batchId, new { count = values.Count });
            await _hub.SaveChangesAsync();

            return values.Select(SecureCode.Format).ToList();
        }

        public async Task<PaginatedResult<PlatformAuditLogDto>> ListAuditLogAsync(PlatformAuditLogQuery query)
        {
            var logs = _hub.AuditLogs.AsNoTracking();
            var totalCount = await logs.CountAsync();

            var items = await logs
                .OrderByDescending(l => l.Id)
                .Skip((query.PageIndex - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(l => new PlatformAuditLogDto
                {
                    Id = l.Id,
                    AccountName = l.Account == null ? null : l.Account.FullName,
                    Action = l.Action,
                    EntityName = l.EntityName,
                    EntityId = l.EntityId,
                    Details = l.Details,
                    CreatedAt = l.CreatedAt,
                })
                .ToListAsync();

            return new PaginatedResult<PlatformAuditLogDto>(totalCount, query.PageSize, query.PageIndex, items);
        }

        // Creates the wanted number of codes and makes sure none of them already exists.
        // A clash is extremely unlikely (31 possible characters in 12 places), but we check anyway
        // because a duplicate code would let two students fight over one payment.
        private async Task<List<string>> CreateUniqueValuesAsync(int count)
        {
            var values = new HashSet<string>();

            while (values.Count < count)
            {
                var candidates = new List<string>();
                while (values.Count + candidates.Count < count)
                    candidates.Add(SecureCode.Generate());

                var alreadyInDatabase = await _hub.Codes
                    .Where(c => candidates.Contains(c.Value))
                    .Select(c => c.Value)
                    .ToListAsync();

                foreach (var candidate in candidates)
                {
                    if (!alreadyInDatabase.Contains(candidate))
                        values.Add(candidate);
                }
            }
            return values.ToList();
        }

        private static CodeState GetState(CodeStatus status, DateTime? redeemBefore, DateTime now)
        {
            if (status == CodeStatus.Used)
                return CodeState.Redeemed;
            if (status == CodeStatus.Voided)
                return CodeState.Revoked;
            if (redeemBefore is not null && redeemBefore <= now)
                return CodeState.Expired;
            return CodeState.Available;
        }

        private void AddAuditLog(string action, string entityName, int entityId, object? details)
        {
            _hub.AuditLogs.Add(new HubAuditLog
            {
                AccountId = _currentUser.AccountId,
                Action = action,
                EntityName = entityName,
                EntityId = entityId.ToString(),
                Details = details is null ? null : JsonSerializer.Serialize(details),
            });
        }

        // PostgreSQL stores times in UTC and refuses a date that does not say which time zone it is in.
        private static DateTime AsUtc(DateTime value)
        {
            return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        }

        private DateTime UtcNow()
        {
            return _clock.GetUtcNow().UtcDateTime;
        }
    }
}
