using ELearning.IntegrationTests.Infrastructure;
using ELearning.Shared.DTOs.Auth;
using ELearningHub.Core.Entities;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace ELearning.IntegrationTests.Auth
{
    // Refresh tokens, logout and expired or changed access tokens.
    [Collection(ApiCollection.Name)]
    public class SessionTests
    {
        private readonly ApiFixture _fixture;
        private readonly TestApi _api;

        public SessionTests(ApiFixture fixture)
        {
            _fixture = fixture;
            _api = new TestApi(fixture);
        }

        private async Task<(string Phone, SessionDto Session)> NewLoggedInStudentAsync()
        {
            var teacher = await _api.SharedTeacherAsync();
            var phone = await _api.AddMemberAsync(teacher.Tenant.Id, MembershipRole.Student);
            var session = await _api.LoginStudentAsync(phone, "phone-A");
            return (phone, session);
        }

        private Task<HttpResponseMessage> RefreshAsync(string refreshToken, string? deviceId = "phone-A", int? tenantId = null)
        {
            return TestApi.PostAsync(_api.Anonymous(), "/api/auth/refresh", new RefreshRequest
            {
                RefreshToken = refreshToken,
                DeviceId = deviceId,
                TenantId = tenantId,
            });
        }

        [Fact]
        public async Task Refresh_gives_a_new_session_with_new_tokens()
        {
            var (_, session) = await NewLoggedInStudentAsync();

            var response = await RefreshAsync(session.RefreshToken);
            var refreshed = await TestApi.ReadAsync<SessionDto>(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotEqual(session.RefreshToken, refreshed.RefreshToken);
            Assert.False(string.IsNullOrEmpty(refreshed.AccessToken));
        }

        [Fact]
        public async Task A_refresh_token_works_only_once_and_reuse_ends_every_session()
        {
            var (_, session) = await NewLoggedInStudentAsync();
            var refreshed = await TestApi.ReadAsync<SessionDto>(await RefreshAsync(session.RefreshToken));

            // The old token appears again: we treat it as stolen.
            var reuse = await RefreshAsync(session.RefreshToken);
            await TestApi.AssertProblemAsync(reuse, HttpStatusCode.Unauthorized, "Auth.InvalidRefreshToken");

            // To be safe, the newer token is cancelled too.
            var newerToken = await RefreshAsync(refreshed.RefreshToken);
            await TestApi.AssertProblemAsync(newerToken, HttpStatusCode.Unauthorized, "Auth.InvalidRefreshToken");
        }

        [Fact]
        public async Task Refresh_from_another_device_is_refused()
        {
            var (_, session) = await NewLoggedInStudentAsync();

            var response = await RefreshAsync(session.RefreshToken, deviceId: "phone-B");

            await TestApi.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "Auth.InvalidRefreshToken");
        }

        [Fact]
        public async Task Unknown_refresh_token_is_refused()
        {
            var response = await RefreshAsync("this-token-does-not-exist");

            await TestApi.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "Auth.InvalidRefreshToken");
        }

        [Fact]
        public async Task Refresh_token_stops_working_after_30_days()
        {
            var (_, session) = await NewLoggedInStudentAsync();

            try
            {
                _fixture.Clock.Offset = TimeSpan.FromDays(31);
                var response = await RefreshAsync(session.RefreshToken);
                await TestApi.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "Auth.InvalidRefreshToken");
            }
            finally
            {
                _fixture.Clock.Offset = TimeSpan.Zero;
            }
        }

        [Fact]
        public async Task Logout_cancels_the_refresh_token()
        {
            var (_, session) = await NewLoggedInStudentAsync();

            var logout = await TestApi.PostAsync(_api.Anonymous(), "/api/auth/logout",
                new LogoutRequest { RefreshToken = session.RefreshToken });
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

            var afterLogout = await RefreshAsync(session.RefreshToken);
            Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
        }

        [Fact]
        public async Task Logout_with_an_unknown_token_still_answers_ok()
        {
            var response = await TestApi.PostAsync(_api.Anonymous(), "/api/auth/logout",
                new LogoutRequest { RefreshToken = "unknown-token" });

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public async Task Only_the_hash_of_a_refresh_token_is_stored()
        {
            var (_, session) = await NewLoggedInStudentAsync();

            var tokenIsStoredAsText = await _api.ReadHubAsync(hub =>
                hub.RefreshTokens.AnyAsync(t => t.TokenHash == session.RefreshToken));

            Assert.False(tokenIsStoredAsText);
        }

        [Fact]
        public async Task Request_without_a_token_is_refused()
        {
            var response = await _api.Anonymous().GetAsync("/api/profile");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task A_changed_token_is_refused()
        {
            var (_, session) = await NewLoggedInStudentAsync();

            // Change one character in the middle part (the data) of the token. The signature no longer matches.
            var parts = session.AccessToken.Split('.');
            var payload = parts[1];
            var changedCharacter = payload[10] == 'A' ? 'B' : 'A';
            parts[1] = payload.Substring(0, 10) + changedCharacter + payload.Substring(11);
            var changedToken = string.Join('.', parts);

            var response = await _api.WithToken(changedToken).GetAsync("/api/profile");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task An_expired_access_token_is_refused()
        {
            SessionDto oldSession;
            try
            {
                // Log in "two hours ago": the access token lives 30 minutes, so it is already expired now.
                _fixture.Clock.Offset = TimeSpan.FromHours(-2);
                (_, oldSession) = await NewLoggedInStudentAsync();
            }
            finally
            {
                _fixture.Clock.Offset = TimeSpan.Zero;
            }

            var response = await _api.WithToken(oldSession.AccessToken).GetAsync("/api/profile");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Disabling_an_account_stops_its_refresh_token()
        {
            var (phone, session) = await NewLoggedInStudentAsync();
            await _api.ChangeHubAsync(async hub =>
            {
                var account = await hub.Accounts.SingleAsync(a => a.PhoneNumber == phone);
                account.IsActive = false;
            });

            var response = await RefreshAsync(session.RefreshToken);

            await TestApi.AssertProblemAsync(response, HttpStatusCode.Forbidden, "Auth.AccountDisabled");
        }
    }
}
