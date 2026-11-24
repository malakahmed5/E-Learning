using ELearning.Core.Contracts;
using ELearning.Services.Abstraction;
using ELearning.Services.Options;
using ELearning.Services.Security;
using ELearning.Shared.CommonResponses;
using ELearning.Shared.DTOs.Codes;
using ELearning.Shared.Validation;
using ELearningHub.Core.Contracts;
using ELearningHub.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Auth = ELearning.Services.Auth;

namespace ELearning.Services.Codes
{
    // A new student opens the account for the first time.
    //
    // The teacher (or assistant) already added the student by phone number, but the account has no password.
    // The student proves they were given access by typing a code, and chooses a password.
    // The same code also gives the first days of access (usually the free week).
    public class ActivationService : IActivationService
    {
        private readonly IHubDbContext _hub;
        private readonly ITenantDatabaseManager _tenantDatabases;
        private readonly CodeRedeemer _redeemer;
        private readonly IPasswordHasher<Account> _passwordHasher;
        private readonly AuthOptions _authOptions;
        private readonly TimeProvider _clock;

        public ActivationService(
            IHubDbContext hub,
            ITenantDatabaseManager tenantDatabases,
            CodeRedeemer redeemer,
            IPasswordHasher<Account> passwordHasher,
            IOptions<AuthOptions> authOptions,
            TimeProvider clock)
        {
            _hub = hub;
            _tenantDatabases = tenantDatabases;
            _redeemer = redeemer;
            _passwordHasher = passwordHasher;
            _authOptions = authOptions.Value;
            _clock = clock;
        }

        public async Task<Result<RedeemResultDto>> ActivateAsync(ActivateAccountRequest request)
        {
            if (!EgyptianPhone.TryNormalize(request.PhoneNumber, out var phoneNumber))
                return CodeErrors.ActivationInvalid;

            if (!PasswordRules.IsStrongEnough(request.Password))
                return Auth.AuthErrors.WeakPassword;

            var account = await _hub.Accounts
                .Include(a => a.Memberships)
                    .ThenInclude(m => m.Tenant)
                .FirstOrDefaultAsync(a => a.PhoneNumber == phoneNumber);

            if (account is null)
                return CodeErrors.ActivationInvalid;

            var now = _clock.GetUtcNow().UtcDateTime;
            if (!account.IsActive)
                return Auth.AuthErrors.AccountDisabled;
            if (AccountLockout.IsLocked(account, now))
                return Auth.AuthErrors.AccountLocked;

            // Activation is only for accounts with no password. Otherwise a code would be a way
            // to replace someone's password, which must never be possible.
            if (account.PasswordHash is not null)
                return CodeErrors.AlreadyActivated;

            var membership = ChooseStudentMembership(account, request.TenantSlug, out var chooseError);
            if (membership is null)
                return chooseError!;

            await using var tenantDatabase = _tenantDatabases.Open(membership.Tenant.ConnectionString);

            var student = await tenantDatabase.Students.FirstOrDefaultAsync(s => s.UserId == membership.TenantUserId);
            if (student is null)
                return CodeErrors.ActivationInvalid;

            // The password is set on the account object now and saved together with the code
            // (the redeemer saves the hub). So "code used" and "password set" happen together.
            account.PasswordHash = _passwordHasher.HashPassword(account, request.Password);

            var result = await _redeemer.RedeemAsync(tenantDatabase, membership.TenantId, account.Id, student, request.Code);
            if (result.IsFailure)
            {
                // The code was not accepted, so the password must NOT be saved.
                account.PasswordHash = null;

                // Count the wrong try, so codes cannot be guessed for a known phone number.
                // A technical failure on our side is not the student's mistake and is not counted.
                var isOurFailure = result.Errors[0].Code == CodeErrors.RedeemFailed.Code;
                if (!isOurFailure)
                    AccountLockout.RegisterFailure(account, _authOptions, now);

                await _hub.SaveChangesAsync();
            }

            return result;
        }

        // Finds which teacher the new student is activating at.
        private static Membership? ChooseStudentMembership(Account account, string? tenantSlug, out Error? error)
        {
            error = null;

            var studentMemberships = account.Memberships
                .Where(m => m.Role == MembershipRole.Student && m.IsActive && m.Tenant.IsActive)
                .ToList();

            if (!string.IsNullOrWhiteSpace(tenantSlug))
            {
                var slug = tenantSlug.Trim().ToLowerInvariant();
                var chosen = studentMemberships.FirstOrDefault(m => m.Tenant.Slug == slug);
                if (chosen is null)
                    error = CodeErrors.ActivationInvalid;
                return chosen;
            }

            if (studentMemberships.Count == 1)
                return studentMemberships[0];

            // No teacher at all looks the same as a wrong phone number. Several teachers: the student must say which.
            error = studentMemberships.Count == 0 ? CodeErrors.ActivationInvalid : CodeErrors.ChooseTeacher;
            return null;
        }
    }
}
