using ELearning.Services.Abstraction;
using ELearning.Services.Options;
using ELearning.Services.Security;
using ELearning.Shared.CommonResponses;
using ELearning.Shared.DTOs.Auth;
using ELearning.Shared.Validation;
using ELearningHub.Core.Contracts;
using ELearningHub.Core.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ELearning.Services.Auth
{
    // Login for everyone: students, assistants, teachers and the platform team.
    //
    // How login works:
    //   1. The user sends phone number and password.
    //   2. Students get a session right away (on their one allowed device).
    //   3. Staff must also send the 6-digit code from their authenticator app.
    //   4. A session has a short access token and a long refresh token.
    //
    // This file has the login steps. AuthService.Sessions.cs has the session code.
    public partial class AuthService : IAuthService
    {
        // Purpose text for encrypting two-factor secrets. Changing it makes old secrets unreadable.
        private const string SecretProtectionPurpose = "ELearning.TwoFactorSecret.v1";

        // A password hash that belongs to nobody. Made once, used only to spend the same time
        // on unknown phone numbers as on real accounts (see LoginAsync).
        private static readonly Lazy<string> DummyPasswordHash = new(() =>
            new PasswordHasher<Account>().HashPassword(new Account(), Guid.NewGuid().ToString()));

        private readonly IHubDbContext _hub;
        private readonly TokenService _tokens;
        private readonly IPasswordHasher<Account> _passwordHasher;
        private readonly IDataProtector _secretProtector;
        private readonly AuthOptions _authOptions;
        private readonly JwtOptions _jwtOptions;
        private readonly TimeProvider _clock;

        public AuthService(
            IHubDbContext hub,
            TokenService tokens,
            IPasswordHasher<Account> passwordHasher,
            IDataProtectionProvider dataProtectionProvider,
            IOptions<AuthOptions> authOptions,
            IOptions<JwtOptions> jwtOptions,
            TimeProvider clock)
        {
            _hub = hub;
            _tokens = tokens;
            _passwordHasher = passwordHasher;
            _secretProtector = dataProtectionProvider.CreateProtector(SecretProtectionPurpose);
            _authOptions = authOptions.Value;
            _jwtOptions = jwtOptions.Value;
            _clock = clock;
        }

        public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request)
        {
            if (!EgyptianPhone.TryNormalize(request.PhoneNumber, out var phoneNumber))
                return AuthErrors.InvalidCredentials;

            var account = await FindAccountAsync(a => a.PhoneNumber == phoneNumber);

            // No account, or an account that has not set a password yet.
            if (account is null || account.PasswordHash is null)
            {
                // Checking a password is slow on purpose. If we answered at once here, an attacker could
                // tell "this phone has no account" from the fast answer. So we do the same slow work anyway.
                _passwordHasher.VerifyHashedPassword(new Account(), DummyPasswordHash.Value, request.Password);
                return AuthErrors.InvalidCredentials;
            }

            var blockedError = GetBlockedError(account);
            if (blockedError is not null)
                return blockedError;

            var passwordCheck = _passwordHasher.VerifyHashedPassword(account, account.PasswordHash, request.Password);
            if (passwordCheck == PasswordVerificationResult.Failed)
            {
                await RegisterFailedAttemptAsync(account);
                return AuthErrors.InvalidCredentials;
            }

            if (RequiresTwoFactor(account))
            {
                // Important: we do NOT reset the failed-attempt counter here.
                // If we did, someone who knows the password could guess two-factor codes forever
                // by logging in again before each lockout. The counter resets only when login is complete.
                return new LoginResponse
                {
                    Status = account.TwoFactorConfirmedAt is null
                        ? LoginStatus.TwoFactorSetupRequired
                        : LoginStatus.TwoFactorRequired,
                    ChallengeToken = _tokens.CreateChallengeToken(account.Id),
                };
            }

            var session = await StartSessionAsync(account, request.DeviceId, tenantId: null);
            if (session.IsFailure)
                return session.Errors.ToList();

            return new LoginResponse { Status = LoginStatus.Authenticated, Session = session.Value };
        }

        public async Task<Result<TwoFactorSetupResponse>> SetupTwoFactorAsync(TwoFactorSetupRequest request)
        {
            var account = await FindAccountByChallengeAsync(request.ChallengeToken);
            if (account is null)
                return AuthErrors.InvalidChallenge;

            // Once two-factor is confirmed, it cannot be replaced with only a password.
            // Otherwise a stolen password would be enough to take over the account.
            if (account.TwoFactorConfirmedAt is not null)
                return AuthErrors.TwoFactorAlreadySetUp;

            var secret = Totp.GenerateSecret();
            account.TwoFactorSecret = _secretProtector.Protect(secret);
            account.TwoFactorLastStep = null;
            await _hub.SaveChangesAsync();

            return new TwoFactorSetupResponse
            {
                Secret = secret,
                OtpAuthUri = Totp.BuildUri(_authOptions.TwoFactorIssuer, account.PhoneNumber, secret),
            };
        }

        public async Task<Result<SessionDto>> VerifyTwoFactorAsync(TwoFactorVerifyRequest request)
        {
            var account = await FindAccountByChallengeAsync(request.ChallengeToken);
            if (account is null)
                return AuthErrors.InvalidChallenge;

            var blockedError = GetBlockedError(account);
            if (blockedError is not null)
                return blockedError;

            if (account.TwoFactorSecret is null)
                return AuthErrors.TwoFactorNotSetUp;

            var secret = _secretProtector.Unprotect(account.TwoFactorSecret);
            var matchedStep = Totp.FindMatchingStep(secret, request.Code, _clock.GetUtcNow());

            // A code that was already used must not work again, even inside its 30 seconds.
            var alreadyUsed = matchedStep is not null
                              && account.TwoFactorLastStep is not null
                              && matchedStep <= account.TwoFactorLastStep;

            if (matchedStep is null || alreadyUsed)
            {
                await RegisterFailedAttemptAsync(account);
                return AuthErrors.InvalidTwoFactorCode;
            }

            account.TwoFactorLastStep = matchedStep;

            // The first correct code proves the authenticator app is set up.
            account.TwoFactorConfirmedAt ??= UtcNow();

            // Staff are not limited to one device, so no device id is needed here.
            return await StartSessionAsync(account, deviceId: null, tenantId: null);
        }

        public async Task<Result> AcceptInviteAsync(AcceptInviteRequest request)
        {
            if (!EgyptianPhone.TryNormalize(request.PhoneNumber, out var phoneNumber))
                return AuthErrors.InvalidInvite;

            if (!PasswordRules.IsStrongEnough(request.Password))
                return AuthErrors.WeakPassword;

            var account = await FindAccountAsync(a => a.PhoneNumber == phoneNumber);
            if (account is null)
                return AuthErrors.InvalidInvite;

            var blockedError = GetBlockedError(account);
            if (blockedError is not null)
                return blockedError;

            var typedCodeHash = SecureCode.Hash(SecureCode.Normalize(request.InviteCode));
            var inviteIsValid = account.InviteCodeHash is not null
                                && account.InviteExpiresAt is not null
                                && account.InviteExpiresAt > UtcNow()
                                && account.InviteCodeHash == typedCodeHash;

            if (!inviteIsValid)
            {
                // Wrong codes count as failed tries, so the code cannot be guessed by trying many.
                await RegisterFailedAttemptAsync(account);
                return AuthErrors.InvalidInvite;
            }

            account.PasswordHash = _passwordHasher.HashPassword(account, request.Password);

            // The invite works only once.
            account.InviteCodeHash = null;
            account.InviteExpiresAt = null;
            account.AccessFailedCount = 0;

            // If this was a password reset, old sessions must stop working.
            await RevokeAllRefreshTokensAsync(account.Id);

            await _hub.SaveChangesAsync();
            return Result.Ok();
        }

        public async Task<Result<TenantBrandingDto>> GetTenantBrandingAsync(string slug)
        {
            var normalizedSlug = slug.Trim().ToLowerInvariant();

            var branding = await _hub.Tenants
                .AsNoTracking()
                .Where(t => t.Slug == normalizedSlug && t.IsActive)
                .Select(t => new TenantBrandingDto
                {
                    Slug = t.Slug,
                    DisplayName = t.DisplayName,
                    LogoUrl = t.LogoUrl,
                })
                .FirstOrDefaultAsync();

            if (branding is null)
                return AuthErrors.TenantNotFound;

            return branding;
        }

        // Staff (teacher, assistant) and the platform team must use two-factor. Students do not.
        private static bool RequiresTwoFactor(Account account)
        {
            if (account.IsPlatformAdmin)
                return true;

            return account.Memberships.Any(m => m.Role != MembershipRole.Student);
        }

        // Returns an error when the account is not allowed to log in right now, or null when it is fine.
        private Error? GetBlockedError(Account account)
        {
            if (!account.IsActive)
                return AuthErrors.AccountDisabled;

            if (AccountLockout.IsLocked(account, UtcNow()))
                return AuthErrors.AccountLocked;

            return null;
        }

        // Counts a wrong try. After too many in a row, the account is locked for a while.
        private async Task RegisterFailedAttemptAsync(Account account)
        {
            AccountLockout.RegisterFailure(account, _authOptions, UtcNow());
            await _hub.SaveChangesAsync();
        }

        private async Task<Account?> FindAccountByChallengeAsync(string challengeToken)
        {
            var accountId = await _tokens.ReadChallengeTokenAsync(challengeToken);
            if (accountId is null)
                return null;

            return await FindAccountAsync(a => a.Id == accountId);
        }

        // Loads an account together with its teachers and its linked device.
        private Task<Account?> FindAccountAsync(System.Linq.Expressions.Expression<Func<Account, bool>> filter)
        {
            return _hub.Accounts
                .Include(a => a.Memberships)
                    .ThenInclude(m => m.Tenant)
                .Include(a => a.Device)
                .FirstOrDefaultAsync(filter);
        }

        private DateTime UtcNow()
        {
            return _clock.GetUtcNow().UtcDateTime;
        }
    }
}
