using ELearning.IntegrationTests.Infrastructure;
using ELearning.Services.Security;
using ELearning.Shared.DTOs.Auth;
using ELearningHub.Core.Entities;
using System.Net;

namespace ELearning.IntegrationTests.Auth
{
    // Two-factor login for staff (teacher, assistant, platform admin).
    [Collection(ApiCollection.Name)]
    public class TwoFactorTests
    {
        private readonly ApiFixture _fixture;
        private readonly TestApi _api;

        public TwoFactorTests(ApiFixture fixture)
        {
            _fixture = fixture;
            _api = new TestApi(fixture);
        }

        [Fact]
        public async Task Platform_admin_gets_a_session_only_after_the_two_factor_step()
        {
            var session = await _api.AdminSessionAsync();

            Assert.True(session.Account.IsPlatformAdmin);
            Assert.Null(session.ActiveMembership);

            var adminList = await _api.WithToken(session.AccessToken).GetAsync("/api/admin/tenants");
            Assert.Equal(HttpStatusCode.OK, adminList.StatusCode);
        }

        [Fact]
        public async Task Password_alone_does_not_give_a_session_to_staff()
        {
            var teacher = await _api.CreateTeacherAsync();

            var response = await _api.LoginAsync(teacher.Login.Phone, teacher.Login.Password);
            var login = await TestApi.ReadAsync<LoginResponse>(response);

            Assert.Equal(LoginStatus.TwoFactorRequired, login.Status);
            Assert.Null(login.Session);
            Assert.False(string.IsNullOrEmpty(login.ChallengeToken));
        }

        [Fact]
        public async Task Teacher_logs_in_with_password_and_code_and_lands_at_own_platform()
        {
            var teacher = await _api.CreateTeacherAsync();

            var session = await _api.LoginStaffAsync(teacher.Login);

            Assert.NotNull(session.ActiveMembership);
            Assert.Equal(teacher.Tenant.Id, session.ActiveMembership!.TenantId);
            Assert.Equal("Teacher", session.ActiveMembership.Role);
        }

        [Fact]
        public async Task Assistant_must_set_up_two_factor_on_first_login()
        {
            var teacher = await _api.CreateTeacherAsync();
            var assistantPhone = await _api.AddMemberAsync(teacher.Tenant.Id, MembershipRole.Assistant);

            var response = await _api.LoginAsync(assistantPhone, TestApi.DefaultPassword);
            var login = await TestApi.ReadAsync<LoginResponse>(response);

            Assert.Equal(LoginStatus.TwoFactorSetupRequired, login.Status);
            Assert.Null(login.Session);
        }

        [Fact]
        public async Task Wrong_code_is_rejected()
        {
            var teacher = await _api.CreateTeacherAsync();
            var login = await TestApi.ReadAsync<LoginResponse>(await _api.LoginAsync(teacher.Login.Phone, teacher.Login.Password));

            // Take the real code and change its last digit, so it is surely wrong.
            var realCode = _api.CurrentCode(teacher.Login.TwoFactorSecret);
            var lastDigit = realCode[5] - '0';
            var wrongCode = realCode.Substring(0, 5) + ((lastDigit + 1) % 10);

            // Make sure the changed code is not by chance the code of a neighbour time step.
            var step = Totp.GetStep(_fixture.Clock.GetUtcNow());
            var neighbours = new[]
            {
                Totp.GetCode(teacher.Login.TwoFactorSecret, step - 1),
                Totp.GetCode(teacher.Login.TwoFactorSecret, step + 1),
            };
            if (neighbours.Contains(wrongCode))
                return;

            var response = await _api.VerifyTwoFactorAsync(login.ChallengeToken!, wrongCode);

            await TestApi.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "Auth.InvalidTwoFactorCode");
        }

        [Fact]
        public async Task The_same_code_cannot_be_used_twice()
        {
            var teacher = await _api.CreateTeacherAsync();

            var firstLogin = await TestApi.ReadAsync<LoginResponse>(await _api.LoginAsync(teacher.Login.Phone, teacher.Login.Password));
            var code = _api.NextCode(teacher.Login.TwoFactorSecret);
            var firstUse = await _api.VerifyTwoFactorAsync(firstLogin.ChallengeToken!, code);
            Assert.Equal(HttpStatusCode.OK, firstUse.StatusCode);

            // Someone who saw the code (for example over the shoulder) tries it again.
            var secondLogin = await TestApi.ReadAsync<LoginResponse>(await _api.LoginAsync(teacher.Login.Phone, teacher.Login.Password));
            var secondUse = await _api.VerifyTwoFactorAsync(secondLogin.ChallengeToken!, code);

            await TestApi.AssertProblemAsync(secondUse, HttpStatusCode.Unauthorized, "Auth.InvalidTwoFactorCode");
        }

        [Fact]
        public async Task Guessing_codes_locks_the_account_even_when_the_password_is_known()
        {
            var teacher = await _api.CreateTeacherAsync();

            // The attacker knows the password and logs in again before every guess.
            for (var attempt = 1; attempt <= 5; attempt++)
            {
                var loginResponse = await _api.LoginAsync(teacher.Login.Phone, teacher.Login.Password);
                var login = await TestApi.ReadAsync<LoginResponse>(loginResponse);
                await _api.VerifyTwoFactorAsync(login.ChallengeToken!, "000000");
            }

            var afterGuesses = await _api.LoginAsync(teacher.Login.Phone, teacher.Login.Password);

            await TestApi.AssertProblemAsync(afterGuesses, HttpStatusCode.Forbidden, "Auth.AccountLocked");
        }

        [Fact]
        public async Task Two_factor_cannot_be_set_up_again_with_only_the_password()
        {
            var teacher = await _api.CreateTeacherAsync();
            var login = await TestApi.ReadAsync<LoginResponse>(await _api.LoginAsync(teacher.Login.Phone, teacher.Login.Password));

            // A thief with the password tries to replace the authenticator secret with their own.
            var response = await TestApi.PostAsync(_api.Anonymous(), "/api/auth/two-factor/setup",
                new TwoFactorSetupRequest { ChallengeToken = login.ChallengeToken! });

            await TestApi.AssertProblemAsync(response, HttpStatusCode.Conflict, "Auth.TwoFactorAlreadySetUp");
        }

        [Fact]
        public async Task Verify_before_setup_is_refused()
        {
            var teacher = await _api.CreateTeacherAsync();
            var assistantPhone = await _api.AddMemberAsync(teacher.Tenant.Id, MembershipRole.Assistant);
            var login = await TestApi.ReadAsync<LoginResponse>(await _api.LoginAsync(assistantPhone, TestApi.DefaultPassword));

            var response = await _api.VerifyTwoFactorAsync(login.ChallengeToken!, "123456");

            await TestApi.AssertProblemAsync(response, HttpStatusCode.Conflict, "Auth.TwoFactorNotSetUp");
        }

        [Fact]
        public async Task A_challenge_token_is_not_a_login_token()
        {
            var admin = await _api.AdminLoginDetailsAsync();
            var login = await TestApi.ReadAsync<LoginResponse>(await _api.LoginAsync(admin.Phone, admin.Password));

            // The challenge token only proves the password step. It must not open any protected endpoint.
            var response = await _api.WithToken(login.ChallengeToken!).GetAsync("/api/admin/tenants");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData("not-a-token")]
        [InlineData("")]
        public async Task A_fake_challenge_token_is_refused(string fakeToken)
        {
            var response = await TestApi.PostAsync(_api.Anonymous(), "/api/auth/two-factor/verify",
                new { challengeToken = fakeToken, code = "123456" });

            Assert.True(
                response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.BadRequest,
                $"Unexpected status {(int)response.StatusCode}");
        }

        [Fact]
        public async Task An_expired_challenge_token_is_refused()
        {
            var teacher = await _api.CreateTeacherAsync();
            var login = await TestApi.ReadAsync<LoginResponse>(await _api.LoginAsync(teacher.Login.Phone, teacher.Login.Password));

            try
            {
                // The challenge is valid for 10 minutes.
                _fixture.Clock.Offset = TimeSpan.FromMinutes(11);
                var response = await _api.VerifyTwoFactorAsync(login.ChallengeToken!, _api.CurrentCode(teacher.Login.TwoFactorSecret));

                await TestApi.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "Auth.InvalidChallenge");
            }
            finally
            {
                _fixture.Clock.Offset = TimeSpan.Zero;
            }
        }

        [Fact]
        public async Task The_two_factor_secret_is_stored_encrypted()
        {
            var teacher = await _api.CreateTeacherAsync();

            var storedSecret = await _api.ReadHubAsync(hub => Task.FromResult(
                hub.Accounts.Where(a => a.PhoneNumber == teacher.Login.Phone).Select(a => a.TwoFactorSecret).Single()));

            Assert.NotNull(storedSecret);
            Assert.NotEqual(teacher.Login.TwoFactorSecret, storedSecret);
            Assert.DoesNotContain(teacher.Login.TwoFactorSecret, storedSecret);
        }
    }
}
