using ELearning.Services.Security;
using ELearning.Shared.CommonResponses;
using ELearning.Shared.DTOs.Auth;
using ELearningHub.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Services.Auth
{
    // The session part of AuthService: starting, refreshing and ending sessions.
    public partial class AuthService
    {
        public async Task<Result<SessionDto>> RefreshAsync(RefreshRequest request)
        {
            var tokenHash = TokenService.HashRefreshToken(request.RefreshToken);
            var storedToken = await _hub.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

            if (storedToken is null)
                return AuthErrors.InvalidRefreshToken;

            if (storedToken.RevokedAt is not null)
            {
                // A refresh token is used once and then replaced. Seeing an old one again means
                // it was probably stolen and copied. To be safe, we end every session of this account.
                await RevokeAllRefreshTokensAsync(storedToken.AccountId);
                await _hub.SaveChangesAsync();
                return AuthErrors.InvalidRefreshToken;
            }

            if (storedToken.ExpiresAt <= UtcNow())
                return AuthErrors.InvalidRefreshToken;

            // A token that was given to one device must come back from the same device.
            if (storedToken.DeviceId is not null && storedToken.DeviceId != request.DeviceId)
                return AuthErrors.InvalidRefreshToken;

            var account = await FindAccountAsync(a => a.Id == storedToken.AccountId);
            if (account is null)
                return AuthErrors.InvalidRefreshToken;

            var blockedError = GetBlockedError(account);
            if (blockedError is not null)
                return blockedError;

            // Replace the old token with a new one ("rotation").
            storedToken.RevokedAt = UtcNow();

            return await StartSessionAsync(account, storedToken.DeviceId, request.TenantId);
        }

        public async Task<Result> LogoutAsync(LogoutRequest request)
        {
            var tokenHash = TokenService.HashRefreshToken(request.RefreshToken);
            var storedToken = await _hub.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

            if (storedToken is not null && storedToken.RevokedAt is null)
            {
                storedToken.RevokedAt = UtcNow();
                await _hub.SaveChangesAsync();
            }

            // We answer "ok" even for an unknown token, so this endpoint reveals nothing.
            return Result.Ok();
        }

        // Creates a new session and saves everything that changed on the account.
        // tenantId: the teacher the user wants. Null means "pick it for me when there is only one".
        private async Task<Result<SessionDto>> StartSessionAsync(Account account, string? deviceId, int? tenantId)
        {
            var memberships = GetUsableMemberships(account);

            if (IsStudentOnly(account))
            {
                var deviceError = BindOrCheckDevice(account, deviceId);
                if (deviceError is not null)
                    return deviceError;
            }

            var activeMembership = ChooseActiveMembership(account, memberships, tenantId, out var chooseError);
            if (chooseError is not null)
                return chooseError;

            var accessToken = _tokens.CreateAccessToken(account, activeMembership);
            var refreshToken = TokenService.CreateRefreshToken();

            _hub.RefreshTokens.Add(new RefreshToken
            {
                AccountId = account.Id,
                TokenHash = refreshToken.Hash,
                DeviceId = deviceId,
                ExpiresAt = UtcNow().AddDays(_jwtOptions.RefreshTokenDays),
            });

            // Login is complete, so the wrong-try counter starts again from zero.
            account.AccessFailedCount = 0;
            account.LockoutEndsAt = null;

            await _hub.SaveChangesAsync();

            return new SessionDto
            {
                AccessToken = accessToken.Value,
                AccessTokenExpiresAt = accessToken.ExpiresAt,
                RefreshToken = refreshToken.Value,
                Account = ToAccountDto(account),
                Memberships = memberships.Select(ToMembershipDto).ToList(),
                ActiveMembership = activeMembership is null ? null : ToMembershipDto(activeMembership),
            };
        }

        // The teachers the user can open now: the membership is active and the teacher is active.
        private static List<Membership> GetUsableMemberships(Account account)
        {
            return account.Memberships
                .Where(m => m.IsActive && m.Tenant.IsActive)
                .OrderBy(m => m.Tenant.DisplayName)
                .ToList();
        }

        private static Membership? ChooseActiveMembership(
            Account account, List<Membership> memberships, int? tenantId, out Error? error)
        {
            error = null;

            if (tenantId is not null)
            {
                var chosen = memberships.FirstOrDefault(m => m.TenantId == tenantId);

                // The user asked for a teacher they do not belong to (or one that is turned off).
                if (chosen is null)
                    error = AuthErrors.TenantNotAvailable;

                return chosen;
            }

            // With exactly one teacher there is nothing to choose, so we open it directly.
            // Platform admins stay outside any teacher unless they ask for one.
            if (memberships.Count == 1 && !account.IsPlatformAdmin)
                return memberships[0];

            return null;
        }

        // True when the account is only a student (at one teacher or many).
        // Only these accounts are limited to one device.
        private static bool IsStudentOnly(Account account)
        {
            if (account.IsPlatformAdmin || account.Memberships.Count == 0)
                return false;

            return account.Memberships.All(m => m.Role == MembershipRole.Student);
        }

        // Business rule: a student account works on one device only, so it cannot be shared.
        // The first device that logs in becomes the linked device.
        private Error? BindOrCheckDevice(Account account, string? deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
                return AuthErrors.DeviceRequired;

            if (account.Device is null)
            {
                account.Device = new AccountDevice
                {
                    AccountId = account.Id,
                    DeviceId = deviceId,
                    BoundAt = UtcNow(),
                };
                return null;
            }

            if (account.Device.DeviceId != deviceId)
                return AuthErrors.DeviceMismatch;

            return null;
        }

        // Ends every open session of an account. Nothing is saved here: the caller saves.
        private async Task RevokeAllRefreshTokensAsync(int accountId)
        {
            var now = UtcNow();
            var openTokens = await _hub.RefreshTokens
                .Where(t => t.AccountId == accountId && t.RevokedAt == null)
                .ToListAsync();

            foreach (var token in openTokens)
                token.RevokedAt = now;
        }

        private static AccountDto ToAccountDto(Account account)
        {
            return new AccountDto
            {
                Id = account.Id,
                FullName = account.FullName,
                PhoneNumber = account.PhoneNumber,
                IsPlatformAdmin = account.IsPlatformAdmin,
            };
        }

        private static MembershipDto ToMembershipDto(Membership membership)
        {
            return new MembershipDto
            {
                TenantId = membership.TenantId,
                TenantSlug = membership.Tenant.Slug,
                TenantName = membership.Tenant.DisplayName,
                TenantLogoUrl = membership.Tenant.LogoUrl,
                Role = membership.Role.ToString(),
            };
        }
    }
}
