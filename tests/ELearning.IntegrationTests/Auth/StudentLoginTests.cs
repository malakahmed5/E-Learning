using ELearning.IntegrationTests.Infrastructure;
using ELearning.Shared.DTOs.Auth;
using ELearningHub.Core.Entities;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace ELearning.IntegrationTests.Auth
{
    // Password login, wrong-password lockout and the one-device rule for students.
    [Collection(ApiCollection.Name)]
    public class StudentLoginTests
    {
        private readonly ApiFixture _fixture;
        private readonly TestApi _api;

        public StudentLoginTests(ApiFixture fixture)
        {
            _fixture = fixture;
            _api = new TestApi(fixture);
        }

        [Fact]
        public async Task Student_with_one_teacher_logs_in_and_lands_at_that_teacher()
        {
            var teacher = await _api.SharedTeacherAsync();
            var phone = await _api.AddMemberAsync(teacher.Tenant.Id, MembershipRole.Student);

            var session = await _api.LoginStudentAsync(phone, deviceId: "phone-A");

            Assert.False(string.IsNullOrEmpty(session.AccessToken));
            Assert.False(string.IsNullOrEmpty(session.RefreshToken));
            Assert.Single(session.Memberships);
            Assert.NotNull(session.ActiveMembership);
            Assert.Equal(teacher.Tenant.Id, session.ActiveMembership!.TenantId);
            Assert.Equal("Student", session.ActiveMembership.Role);
        }

        [Fact]
        public async Task Wrong_password_and_unknown_phone_give_the_same_answer()
        {
            var teacher = await _api.SharedTeacherAsync();
            var phone = await _api.AddMemberAsync(teacher.Tenant.Id, MembershipRole.Student);

            var wrongPassword = await _api.LoginAsync(phone, "WrongPassword1", "phone-A");
            var unknownPhone = await _api.LoginAsync(TestApi.NextPhone(), TestApi.DefaultPassword, "phone-A");

            // Same status and same error code, so nobody can find out which phone numbers have accounts.
            await TestApi.AssertProblemAsync(wrongPassword, HttpStatusCode.Unauthorized, "Auth.InvalidCredentials");
            await TestApi.AssertProblemAsync(unknownPhone, HttpStatusCode.Unauthorized, "Auth.InvalidCredentials");
        }

        [Fact]
        public async Task Phone_number_can_be_typed_with_the_country_code()
        {
            var teacher = await _api.SharedTeacherAsync();
            var phone = await _api.AddMemberAsync(teacher.Tenant.Id, MembershipRole.Student);
            var withCountryCode = "+2" + phone;

            var response = await _api.LoginAsync(withCountryCode, TestApi.DefaultPassword, "phone-A");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Theory]
        [InlineData("12345")]
        [InlineData("' OR '1'='1")]
        [InlineData("01012345678; DROP TABLE accounts;--")]
        public async Task Invalid_phone_number_text_is_rejected_before_any_database_work(string badPhone)
        {
            var response = await _api.LoginAsync(badPhone, TestApi.DefaultPassword, "phone-A");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Account_is_locked_after_five_wrong_passwords_and_opens_again_later()
        {
            var teacher = await _api.SharedTeacherAsync();
            var phone = await _api.AddMemberAsync(teacher.Tenant.Id, MembershipRole.Student);

            for (var attempt = 1; attempt <= 5; attempt++)
            {
                var wrong = await _api.LoginAsync(phone, "WrongPassword1", "phone-A");
                Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
            }

            // Even the correct password is refused while the account is locked.
            var whileLocked = await _api.LoginAsync(phone, TestApi.DefaultPassword, "phone-A");
            await TestApi.AssertProblemAsync(whileLocked, HttpStatusCode.Forbidden, "Auth.AccountLocked");

            try
            {
                _fixture.Clock.Offset = TimeSpan.FromMinutes(16);
                var afterLockout = await _api.LoginAsync(phone, TestApi.DefaultPassword, "phone-A");
                Assert.Equal(HttpStatusCode.OK, afterLockout.StatusCode);
            }
            finally
            {
                _fixture.Clock.Offset = TimeSpan.Zero;
            }
        }

        [Fact]
        public async Task Four_wrong_passwords_then_a_correct_one_resets_the_counter()
        {
            var teacher = await _api.SharedTeacherAsync();
            var phone = await _api.AddMemberAsync(teacher.Tenant.Id, MembershipRole.Student);

            for (var attempt = 1; attempt <= 4; attempt++)
                await _api.LoginAsync(phone, "WrongPassword1", "phone-A");

            var correct = await _api.LoginAsync(phone, TestApi.DefaultPassword, "phone-A");
            Assert.Equal(HttpStatusCode.OK, correct.StatusCode);

            var failedCount = await _api.ReadHubAsync(hub =>
                hub.Accounts.Where(a => a.PhoneNumber == phone).Select(a => a.AccessFailedCount).SingleAsync());
            Assert.Equal(0, failedCount);
        }

        [Fact]
        public async Task Disabled_account_cannot_log_in()
        {
            var teacher = await _api.SharedTeacherAsync();
            var phone = await _api.AddMemberAsync(teacher.Tenant.Id, MembershipRole.Student);
            await _api.ChangeHubAsync(async hub =>
            {
                var account = await hub.Accounts.SingleAsync(a => a.PhoneNumber == phone);
                account.IsActive = false;
            });

            var response = await _api.LoginAsync(phone, TestApi.DefaultPassword, "phone-A");

            await TestApi.AssertProblemAsync(response, HttpStatusCode.Forbidden, "Auth.AccountDisabled");
        }

        [Fact]
        public async Task Student_must_send_a_device_id()
        {
            var teacher = await _api.SharedTeacherAsync();
            var phone = await _api.AddMemberAsync(teacher.Tenant.Id, MembershipRole.Student);

            var response = await _api.LoginAsync(phone, TestApi.DefaultPassword, deviceId: null);

            await TestApi.AssertProblemAsync(response, HttpStatusCode.BadRequest, "Auth.DeviceRequired");
        }

        [Fact]
        public async Task Student_account_works_on_the_first_device_only()
        {
            var teacher = await _api.SharedTeacherAsync();
            var phone = await _api.AddMemberAsync(teacher.Tenant.Id, MembershipRole.Student);

            // The first device becomes the linked device.
            await _api.LoginStudentAsync(phone, deviceId: "phone-A");

            var otherDevice = await _api.LoginAsync(phone, TestApi.DefaultPassword, "phone-B");
            await TestApi.AssertProblemAsync(otherDevice, HttpStatusCode.Forbidden, "Auth.DeviceMismatch");

            // The linked device keeps working.
            var sameDevice = await _api.LoginAsync(phone, TestApi.DefaultPassword, "phone-A");
            Assert.Equal(HttpStatusCode.OK, sameDevice.StatusCode);
        }

        [Fact]
        public async Task Account_without_a_password_cannot_log_in()
        {
            var teacher = await _api.SharedTeacherAsync();
            var phone = await _api.AddMemberAsync(teacher.Tenant.Id, MembershipRole.Student);
            await _api.ChangeHubAsync(async hub =>
            {
                var account = await hub.Accounts.SingleAsync(a => a.PhoneNumber == phone);
                account.PasswordHash = null;
            });

            var response = await _api.LoginAsync(phone, TestApi.DefaultPassword, "phone-A");

            await TestApi.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "Auth.InvalidCredentials");
        }

        [Fact]
        public async Task Login_response_never_contains_the_password_hash()
        {
            var teacher = await _api.SharedTeacherAsync();
            var phone = await _api.AddMemberAsync(teacher.Tenant.Id, MembershipRole.Student);

            var response = await _api.LoginAsync(phone, TestApi.DefaultPassword, "phone-A");
            var body = await response.Content.ReadAsStringAsync();

            Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("connectionString", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("twoFactorSecret", body, StringComparison.OrdinalIgnoreCase);
        }
    }
}
