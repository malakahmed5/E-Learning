using ELearning.Core.Contracts;
using ELearning.Core.Entities.Students;
using ELearning.Core.Entities.Subscriptions;
using ELearning.Services.Security;
using ELearning.Shared.CommonResponses;
using ELearning.Shared.DTOs.Codes;
using ELearningHub.Core.Contracts;
using ELearningHub.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ELearning.Services.Codes
{
    // The one place where a code is turned into days of access.
    // Used by two flows: a logged-in student entering a code, and a new student opening the account.
    //
    // A code lives in the hub database and the student's days live in the teacher's database.
    // The two cannot be saved in one transaction, so the steps are done in a safe order:
    //   1. Check the code (read only).
    //   2. Claim the code in the hub with ONE database command that only works if it is still unused.
    //      If two people send the same code at the same moment, the database lets only one of them win.
    //   3. Add the days in the teacher's database.
    //   4. If step 3 fails, give the code back (make it unused again), so nobody loses a paid code.
    public class CodeRedeemer
    {
        private readonly IHubDbContext _hub;
        private readonly TimeProvider _clock;
        private readonly ILogger<CodeRedeemer> _logger;

        public CodeRedeemer(IHubDbContext hub, TimeProvider clock, ILogger<CodeRedeemer> logger)
        {
            _hub = hub;
            _clock = clock;
            _logger = logger;
        }

        // tenantDatabase: the teacher's database the student belongs to.
        // student: must be loaded from that same database (it is changed and saved here).
        public async Task<Result<RedeemResultDto>> RedeemAsync(
            IELearningDbContext tenantDatabase, int tenantId, int accountId, Student student, string? typedCode)
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            // ----- Step 1: check the code -----
            var codeValue = SecureCode.Normalize(typedCode);
            if (codeValue.Length != SecureCode.Length)
                return CodeErrors.Invalid;

            var code = await _hub.Codes
                .AsNoTracking()
                .Include(c => c.Batch)
                .FirstOrDefaultAsync(c => c.Value == codeValue);

            if (code is null)
                return CodeErrors.Invalid;
            if (code.Status == CodeStatus.Voided)
                return CodeErrors.Revoked;
            if (code.Status == CodeStatus.Used)
                return CodeErrors.AlreadyUsed;
            if (code.Batch.RedeemBefore is not null && code.Batch.RedeemBefore <= now)
                return CodeErrors.Expired;

            var isFreeWeek = code.Batch.Type == CodeType.FreeWeek;

            // Business rule: the free week is given once per student at each teacher.
            if (isFreeWeek)
            {
                var hadFreeWeek = await tenantDatabase.SubscriptionPeriods
                    .AnyAsync(p => p.StudentId == student.Id && p.Type == PeriodType.Trial);
                if (hadFreeWeek)
                    return CodeErrors.FreeWeekAlreadyUsed;
            }

            // ----- Step 2: claim the code -----
            // "WHERE status = Available" is what makes this safe: only one request can change the row.
            var claimedRows = await _hub.Codes
                .Where(c => c.Id == code.Id && c.Status == CodeStatus.Available)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(c => c.Status, CodeStatus.Used)
                    .SetProperty(c => c.UsedByAccountId, (int?)accountId)
                    .SetProperty(c => c.UsedInTenantId, (int?)tenantId)
                    .SetProperty(c => c.UsedAt, (DateTime?)now)
                    .SetProperty(c => c.UpdatedAt, (DateTime?)now));

            if (claimedRows == 0)
                return CodeErrors.AlreadyUsed;

            // ----- Step 3: add the days in the teacher's database -----
            // Days are added after any days the student still has, so renewing early loses nothing.
            var stillHasDays = student.AccessEndsAt is not null && student.AccessEndsAt > now;
            var startsAt = stillHasDays ? student.AccessEndsAt!.Value : now;
            var endsAt = startsAt.AddDays(code.Batch.DurationDays);

            try
            {
                tenantDatabase.SubscriptionPeriods.Add(new SubscriptionPeriod
                {
                    StudentId = student.Id,
                    Type = isFreeWeek ? PeriodType.Trial : PeriodType.Paid,
                    Source = isFreeWeek ? PeriodSource.ActivationCode : PeriodSource.SubscriptionCode,
                    StartsAt = startsAt,
                    EndsAt = endsAt,
                    HubCodeId = code.Id,
                });

                student.AccessEndsAt = endsAt;
                student.ActivatedAt ??= now;
                student.FailedCodeAttempts = 0;
                student.CodeLockoutEndsAt = null;

                await tenantDatabase.SaveChangesAsync();
            }
            catch (Exception exception)
            {
                // ----- Step 4: give the code back -----
                _logger.LogError(exception, "Adding days failed after code {CodeId} was claimed; returning the code", code.Id);

                await _hub.Codes
                    .Where(c => c.Id == code.Id && c.UsedByAccountId == accountId)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(c => c.Status, CodeStatus.Available)
                        .SetProperty(c => c.UsedByAccountId, (int?)null)
                        .SetProperty(c => c.UsedInTenantId, (int?)null)
                        .SetProperty(c => c.UsedAt, (DateTime?)null));

                return CodeErrors.RedeemFailed;
            }

            // Record who used which code. The code text itself is not written to the log.
            _hub.AuditLogs.Add(new HubAuditLog
            {
                AccountId = accountId,
                Action = "CodeRedeemed",
                EntityName = nameof(Code),
                EntityId = code.Id.ToString(),
                Details = JsonSerializer.Serialize(new { tenantId, type = code.Batch.Type.ToString(), endsAt }),
            });
            await _hub.SaveChangesAsync();

            return new RedeemResultDto
            {
                Type = isFreeWeek ? CodeKind.FreeWeek : CodeKind.Subscription30,
                DurationDays = code.Batch.DurationDays,
                StartsAt = startsAt,
                EndsAt = endsAt,
            };
        }
    }
}
