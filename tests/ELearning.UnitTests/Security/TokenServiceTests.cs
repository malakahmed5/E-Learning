using ELearning.Services.Options;
using ELearning.Services.Security;
using ELearning.Shared.Security;
using ELearningHub.Core.Entities;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ELearning.UnitTests.Security
{
    public class TokenServiceTests
    {
        private readonly TestClock _clock = new(new DateTimeOffset(2027, 9, 1, 10, 0, 0, TimeSpan.Zero));
        private readonly TokenService _tokens;

        public TokenServiceTests()
        {
            var options = Microsoft.Extensions.Options.Options.Create(new JwtOptions
            {
                SecretKey = "unit-test-signing-key-that-is-long-enough-123456",
                Issuer = "test-issuer",
                Audience = "test-audience",
                AccessTokenMinutes = 30,
                ChallengeTokenMinutes = 10,
            });
            _tokens = new TokenService(options, _clock);
        }

        [Fact]
        public void Access_token_without_a_teacher_has_no_teacher_claims()
        {
            var account = new Account { Id = 7, IsPlatformAdmin = false };

            var accessToken = _tokens.CreateAccessToken(account, membership: null);
            var decoded = new JsonWebToken(accessToken.Value);

            Assert.Equal("7", decoded.GetClaim(AppClaims.AccountId).Value);
            Assert.Equal("false", decoded.GetClaim(AppClaims.IsPlatformAdmin).Value);
            Assert.False(decoded.TryGetClaim(AppClaims.TenantId, out _));
            Assert.False(decoded.TryGetClaim(AppClaims.Role, out _));
        }

        [Fact]
        public void Access_token_with_a_teacher_carries_teacher_role_and_user_id()
        {
            var account = new Account { Id = 7 };
            var membership = new Membership { TenantId = 3, TenantUserId = 15, Role = MembershipRole.Assistant };

            var accessToken = _tokens.CreateAccessToken(account, membership);
            var decoded = new JsonWebToken(accessToken.Value);

            Assert.Equal("3", decoded.GetClaim(AppClaims.TenantId).Value);
            Assert.Equal("15", decoded.GetClaim(AppClaims.TenantUserId).Value);
            Assert.Equal("Assistant", decoded.GetClaim(AppClaims.Role).Value);
        }

        [Fact]
        public void Access_token_expires_after_the_configured_minutes()
        {
            var accessToken = _tokens.CreateAccessToken(new Account { Id = 1 }, membership: null);

            Assert.Equal(_clock.GetUtcNow().UtcDateTime.AddMinutes(30), accessToken.ExpiresAt);
        }

        [Fact]
        public async Task Challenge_token_gives_back_the_account_id()
        {
            var challenge = _tokens.CreateChallengeToken(accountId: 42);

            var accountId = await _tokens.ReadChallengeTokenAsync(challenge);

            Assert.Equal(42, accountId);
        }

        [Fact]
        public async Task Challenge_token_stops_working_after_it_expires()
        {
            var challenge = _tokens.CreateChallengeToken(accountId: 42);

            _clock.Advance(TimeSpan.FromMinutes(11));
            var accountId = await _tokens.ReadChallengeTokenAsync(challenge);

            Assert.Null(accountId);
        }

        [Fact]
        public async Task An_access_token_cannot_be_used_as_a_challenge_token()
        {
            var accessToken = _tokens.CreateAccessToken(new Account { Id = 42 }, membership: null);

            var accountId = await _tokens.ReadChallengeTokenAsync(accessToken.Value);

            Assert.Null(accountId);
        }

        [Fact]
        public void A_challenge_token_is_made_for_a_different_audience_than_an_access_token()
        {
            var challenge = new JsonWebToken(_tokens.CreateChallengeToken(accountId: 42));
            var access = new JsonWebToken(_tokens.CreateAccessToken(new Account { Id = 42 }, null).Value);

            // The API only accepts the access-token audience, so a challenge token is refused as a login token.
            Assert.Equal("test-audience", access.Audiences.Single());
            Assert.NotEqual(access.Audiences.Single(), challenge.Audiences.Single());
        }

        [Theory]
        [InlineData("")]
        [InlineData("not-a-token")]
        [InlineData("aaa.bbb.ccc")]
        public async Task Garbage_text_is_not_a_valid_challenge_token(string garbage)
        {
            Assert.Null(await _tokens.ReadChallengeTokenAsync(garbage));
        }

        [Fact]
        public async Task A_token_signed_with_another_key_is_rejected()
        {
            var otherOptions = Microsoft.Extensions.Options.Options.Create(new JwtOptions
            {
                SecretKey = "a-completely-different-signing-key-0987654321-xyz",
                Issuer = "test-issuer",
                Audience = "test-audience",
            });
            var attackerTokens = new TokenService(otherOptions, _clock);
            var forged = attackerTokens.CreateChallengeToken(accountId: 42);

            Assert.Null(await _tokens.ReadChallengeTokenAsync(forged));
        }

        [Fact]
        public void Refresh_token_is_random_and_only_its_hash_is_meant_to_be_stored()
        {
            var first = TokenService.CreateRefreshToken();
            var second = TokenService.CreateRefreshToken();

            Assert.NotEqual(first.Value, second.Value);
            Assert.NotEqual(first.Value, first.Hash);
            Assert.Equal(64, first.Hash.Length);
            Assert.Equal(first.Hash, TokenService.HashRefreshToken(first.Value));
        }
    }
}
