using ELearning.Core.Contracts;
using ELearning.Core.Entities.Students;
using ELearning.Core.Entities.Subscriptions;
using ELearning.Services.Abstraction;
using ELearning.Shared.CommonResponses;
using ELearning.Shared.DTOs.Codes;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Services.Codes
{
    // A logged-in student at the chosen teacher: enter a code, and see how many days are left.
    public class SubscriptionService : ISubscriptionService
    {
        // After this many wrong codes in a row, entering codes is blocked for a while.
        // This stops someone from finding a valid code by trying many.
        private const int MaxWrongCodes = 5;
        private const int LockoutMinutes = 15;

        // The student is reminded to renew in the last 3 days.
        private const int ExpiringSoonDays = 3;

        private readonly IELearningDbContext _database;
        private readonly CodeRedeemer _redeemer;
        private readonly ICurrentUser _currentUser;
        private readonly TimeProvider _clock;

        public SubscriptionService(IELearningDbContext database, CodeRedeemer redeemer, ICurrentUser currentUser, TimeProvider clock)
        {
            _database = database;
            _redeemer = redeemer;
            _currentUser = currentUser;
            _clock = clock;
        }

        public async Task<Result<RedeemResultDto>> RedeemAsync(RedeemCodeRequest request)
        {
            // The student is found from the login token, so a student can only ever add days to themselves.
            var student = await FindMyStudentAsync();
            if (student is null)
                return CodeErrors.StudentNotFound;

            var now = UtcNow();
            if (student.CodeLockoutEndsAt is not null && student.CodeLockoutEndsAt > now)
                return CodeErrors.TooManyAttempts;

            var result = await _redeemer.RedeemAsync(
                _database, _currentUser.TenantId!.Value, _currentUser.AccountId, student, request.Code);

            if (result.IsFailure && IsWrongCode(result.Errors[0]))
                await RegisterWrongCodeAsync(student, now);

            return result;
        }

        public async Task<Result<SubscriptionStatusDto>> GetMyStatusAsync()
        {
            var student = await FindMyStudentAsync();
            if (student is null)
                return CodeErrors.StudentNotFound;

            var now = UtcNow();

            // The most recent period tells us whether the student is in the free week or in paid days.
            var latestPeriodType = await _database.SubscriptionPeriods
                .Where(p => p.StudentId == student.Id)
                .OrderByDescending(p => p.EndsAt)
                .Select(p => (PeriodType?)p.Type)
                .FirstOrDefaultAsync();

            return SubscriptionRules.GetStatus(student.AccessEndsAt, latestPeriodType, now, ExpiringSoonDays);
        }

        private Task<Student?> FindMyStudentAsync()
        {
            var userId = _currentUser.TenantUserId;
            return _database.Students.FirstOrDefaultAsync(s => s.UserId == userId);
        }

        // Only answers that mean "this code does not work" count as a wrong try.
        // A technical failure on our side must not push the student towards a lockout.
        private static bool IsWrongCode(Error error)
        {
            return error.Code == CodeErrors.Invalid.Code
                   || error.Code == CodeErrors.AlreadyUsed.Code
                   || error.Code == CodeErrors.Expired.Code
                   || error.Code == CodeErrors.Revoked.Code;
        }

        private async Task RegisterWrongCodeAsync(Student student, DateTime now)
        {
            student.FailedCodeAttempts++;

            if (student.FailedCodeAttempts >= MaxWrongCodes)
            {
                student.CodeLockoutEndsAt = now.AddMinutes(LockoutMinutes);
                student.FailedCodeAttempts = 0;
            }

            await _database.SaveChangesAsync();
        }

        private DateTime UtcNow()
        {
            return _clock.GetUtcNow().UtcDateTime;
        }
    }

    // Pure calculation, kept separate so it can be unit tested without a database.
    public static class SubscriptionRules
    {
        public static SubscriptionStatusDto GetStatus(DateTime? accessEndsAt, PeriodType? latestPeriodType, DateTime now, int expiringSoonDays)
        {
            if (accessEndsAt is null || latestPeriodType is null)
                return new SubscriptionStatusDto { Status = SubscriptionState.NotActivated };

            if (accessEndsAt <= now)
                return new SubscriptionStatusDto { Status = SubscriptionState.Expired, EndsAt = accessEndsAt };

            var timeLeft = accessEndsAt.Value - now;

            return new SubscriptionStatusDto
            {
                Status = latestPeriodType == PeriodType.Trial ? SubscriptionState.Trial : SubscriptionState.Active,
                EndsAt = accessEndsAt,
                // Rounded up: with 2 hours left we still say "1 day left", not "0 days".
                DaysLeft = (int)Math.Ceiling(timeLeft.TotalDays),
                ExpiringSoon = timeLeft <= TimeSpan.FromDays(expiringSoonDays),
            };
        }
    }
}
