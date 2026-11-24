using ELearning.Services.Options;
using ELearning.Shared.Security;
using ELearningHub.Core.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;

namespace ELearning.Services.Security
{
    public record AccessToken(string Value, DateTime ExpiresAt);

    // Value is given to the user. Hash is what we store in the database.
    public record NewRefreshToken(string Value, string Hash);

    // Creates the three kinds of tokens used in login:
    //   1. Access token:    sent with every request, short-lived, says who the user is.
    //   2. Refresh token:   long-lived random text, used only to get a new access token.
    //   3. Challenge token: proves the password step passed, used only for the two-factor step.
    public class TokenService
    {
        // A challenge token is made for a different "audience" than an access token.
        // This is what stops someone from using a challenge token as a normal login token:
        // the API only accepts tokens made for the normal audience.
        private const string ChallengeAudienceSuffix = ":two-factor";

        private readonly JwtOptions _options;
        private readonly TimeProvider _clock;
        private readonly SigningCredentials _signingCredentials;
        private readonly SymmetricSecurityKey _signingKey;
        private readonly JsonWebTokenHandler _handler = new();

        public TokenService(IOptions<JwtOptions> options, TimeProvider clock)
        {
            _options = options.Value;
            _clock = clock;
            _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
            _signingCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        }

        // membership is the teacher the user chose. Null means "no teacher chosen yet".
        public AccessToken CreateAccessToken(Account account, Membership? membership)
        {
            var claims = new Dictionary<string, object>
            {
                [AppClaims.AccountId] = account.Id.ToString(),
                [AppClaims.IsPlatformAdmin] = account.IsPlatformAdmin ? "true" : "false",
            };

            if (membership is not null)
            {
                claims[AppClaims.TenantId] = membership.TenantId.ToString();
                claims[AppClaims.TenantUserId] = membership.TenantUserId.ToString();
                claims[AppClaims.Role] = membership.Role.ToString();
            }

            var expiresAt = Now().AddMinutes(_options.AccessTokenMinutes);
            var token = CreateSignedToken(claims, _options.Audience, expiresAt);
            return new AccessToken(token, expiresAt);
        }

        public string CreateChallengeToken(int accountId)
        {
            var claims = new Dictionary<string, object>
            {
                [AppClaims.AccountId] = accountId.ToString(),
            };

            var expiresAt = Now().AddMinutes(_options.ChallengeTokenMinutes);
            return CreateSignedToken(claims, _options.Audience + ChallengeAudienceSuffix, expiresAt);
        }

        // Returns the account id inside a challenge token, or null when the token is
        // fake, expired, or not a challenge token.
        public async Task<int?> ReadChallengeTokenAsync(string token)
        {
            var parameters = new TokenValidationParameters
            {
                ValidIssuer = _options.Issuer,
                ValidAudience = _options.Audience + ChallengeAudienceSuffix,
                IssuerSigningKey = _signingKey,
                ValidateIssuerSigningKey = true,
                // We check the expiry with our own clock so tests can control the time.
                LifetimeValidator = (notBefore, expires, securityToken, validationParameters) =>
                    expires is not null && expires.Value > Now(),
            };

            var result = await _handler.ValidateTokenAsync(token, parameters);
            if (!result.IsValid)
                return null;

            var accountIdText = result.Claims.TryGetValue(AppClaims.AccountId, out var value) ? value?.ToString() : null;
            return int.TryParse(accountIdText, out var accountId) ? accountId : null;
        }

        // A refresh token is just long random text. It has no meaning by itself:
        // we look it up in the database (by its hash) to know who it belongs to.
        public static NewRefreshToken CreateRefreshToken()
        {
            var randomBytes = RandomNumberGenerator.GetBytes(32);
            var value = Base64UrlEncoder.Encode(randomBytes);
            return new NewRefreshToken(value, HashRefreshToken(value));
        }

        public static string HashRefreshToken(string value)
        {
            return SecureCode.Hash(value);
        }

        private string CreateSignedToken(Dictionary<string, object> claims, string audience, DateTime expiresAt)
        {
            var now = Now();
            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = _options.Issuer,
                Audience = audience,
                Claims = claims,
                IssuedAt = now,
                NotBefore = now,
                Expires = expiresAt,
                SigningCredentials = _signingCredentials,
            };
            return _handler.CreateToken(descriptor);
        }

        private DateTime Now()
        {
            return _clock.GetUtcNow().UtcDateTime;
        }
    }
}
