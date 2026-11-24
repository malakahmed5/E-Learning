using ELearning.IntegrationTests.Infrastructure;
using ELearning.Shared.DTOs.Auth;
using ELearning.Shared.DTOs.Codes;
using ELearning.Shared.DTOs.Teaching;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace ELearning.IntegrationTests.Codes
{
    // A new student (added by the teacher, no password yet) opens the account with a code.
    [Collection(ApiCollection.Name)]
    public class AccountActivationTests
    {
        private const string NewPassword = "MyFirstPass1";

        private readonly TestApi _api;

        public AccountActivationTests(ApiFixture fixture)
        {
            _api = new TestApi(fixture);
        }

        // A student the teacher has added, who has never logged in and has no password.
        private async Task<StudentDto> NewUnactivatedStudentAsync(CreatedTeacher teacher, string? phone = null)
        {
            var teacherClient = _api.TeacherClient(teacher);
            var group = await _api.CreateGroupAsync(teacherClient);
            return await _api.CreateStudentAsync(teacherClient, group.Id, phone);
        }

        private Task<HttpResponseMessage> ActivateAsync(string phone, string code, string password = NewPassword, string? tenantSlug = null)
        {
            return TestApi.PostAsync(_api.Anonymous(), "/api/auth/activate", new ActivateAccountRequest
            {
                PhoneNumber = phone,
                Code = code,
                Password = password,
                TenantSlug = tenantSlug,
            });
        }

        private async Task<bool> HasPasswordAsync(string phone)
        {
            return await _api.ReadHubAsync(hub => hub.Accounts.AnyAsync(a => a.PhoneNumber == phone && a.PasswordHash != null));
        }

        [Fact]
        public async Task A_new_student_opens_the_account_with_a_free_week_code_and_can_then_log_in()
        {
            var teacher = await _api.CreateTeacherAsync();
            var student = await NewUnactivatedStudentAsync(teacher);

            var response = await ActivateAsync(student.PhoneNumber, await _api.NewCodeAsync(CodeKind.FreeWeek));
            var result = await TestApi.ReadAsync<RedeemResultDto>(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(CodeKind.FreeWeek, result.Type);
            Assert.Equal(TimeSpan.FromDays(7), result.EndsAt - result.StartsAt);

            // The chosen password works, and the student is inside the free week.
            var session = await _api.LoginStudentAsync(student.PhoneNumber, "phone-A", NewPassword);
            var status = await TestApi.SubscriptionStatusAsync(_api.WithToken(session.AccessToken));
            Assert.Equal(SubscriptionState.Trial, status.Status);
            Assert.Equal(7, status.DaysLeft);
        }

        [Fact]
        public async Task A_30_day_code_also_opens_a_new_account()
        {
            var teacher = await _api.SharedTeacherAsync();
            var student = await NewUnactivatedStudentAsync(teacher);

            var result = await TestApi.ReadAsync<RedeemResultDto>(
                await ActivateAsync(student.PhoneNumber, await _api.NewCodeAsync(CodeKind.Subscription30)));

            Assert.Equal(CodeKind.Subscription30, result.Type);
            Assert.Equal(TimeSpan.FromDays(30), result.EndsAt - result.StartsAt);
        }

        [Fact]
        public async Task A_wrong_code_does_not_set_a_password_and_does_not_open_the_account()
        {
            var teacher = await _api.SharedTeacherAsync();
            var student = await NewUnactivatedStudentAsync(teacher);

            var response = await ActivateAsync(student.PhoneNumber, "AAAA-BBBB-CCCC");

            await TestApi.AssertProblemAsync(response, HttpStatusCode.BadRequest, "Code.Invalid");
            Assert.False(await HasPasswordAsync(student.PhoneNumber));

            var login = await _api.LoginAsync(student.PhoneNumber, NewPassword, "phone-A");
            Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        }

        [Fact]
        public async Task Guessing_codes_for_a_known_phone_number_locks_the_account()
        {
            var teacher = await _api.SharedTeacherAsync();
            var student = await NewUnactivatedStudentAsync(teacher);
            var goodCode = await _api.NewCodeAsync(CodeKind.FreeWeek);

            for (var attempt = 1; attempt <= 5; attempt++)
                await ActivateAsync(student.PhoneNumber, "AAAA-BBBB-CCC" + attempt);

            // Even the right code is refused while the account is locked.
            var whileLocked = await ActivateAsync(student.PhoneNumber, goodCode);

            await TestApi.AssertProblemAsync(whileLocked, HttpStatusCode.Forbidden, "Auth.AccountLocked");
            Assert.False(await HasPasswordAsync(student.PhoneNumber));
        }

        [Fact]
        public async Task An_account_that_already_has_a_password_cannot_be_activated_again()
        {
            var teacher = await _api.SharedTeacherAsync();
            var student = await NewUnactivatedStudentAsync(teacher);
            await ActivateAsync(student.PhoneNumber, await _api.NewCodeAsync(CodeKind.FreeWeek));

            // Someone who has a valid code tries to take over the account by "activating" it with a new password.
            var takeover = await ActivateAsync(student.PhoneNumber, await _api.NewCodeAsync(CodeKind.Subscription30), "Attacker123");

            await TestApi.AssertProblemAsync(takeover, HttpStatusCode.Conflict, "Activation.AlreadyActivated");

            // The real password still works and the attacker's does not.
            Assert.Equal(HttpStatusCode.OK, (await _api.LoginAsync(student.PhoneNumber, NewPassword, "phone-A")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await _api.LoginAsync(student.PhoneNumber, "Attacker123", "phone-A")).StatusCode);
        }

        [Fact]
        public async Task One_code_cannot_open_two_accounts()
        {
            var teacher = await _api.SharedTeacherAsync();
            var first = await NewUnactivatedStudentAsync(teacher);
            var second = await NewUnactivatedStudentAsync(teacher);
            var code = await _api.NewCodeAsync(CodeKind.FreeWeek);

            var firstResponse = await ActivateAsync(first.PhoneNumber, code);
            var secondResponse = await ActivateAsync(second.PhoneNumber, code);

            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
            await TestApi.AssertProblemAsync(secondResponse, HttpStatusCode.Conflict, "Code.AlreadyUsed");
            Assert.False(await HasPasswordAsync(second.PhoneNumber));
        }

        [Theory]
        [InlineData("short1")]
        [InlineData("onlyletters")]
        [InlineData("12345678")]
        public async Task A_weak_password_is_refused_and_the_code_is_not_used_up(string weakPassword)
        {
            var teacher = await _api.SharedTeacherAsync();
            var student = await NewUnactivatedStudentAsync(teacher);
            var code = await _api.NewCodeAsync(CodeKind.FreeWeek);

            var weak = await ActivateAsync(student.PhoneNumber, code, weakPassword);
            Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

            // The same code still works with a good password.
            var good = await ActivateAsync(student.PhoneNumber, code);
            Assert.Equal(HttpStatusCode.OK, good.StatusCode);
        }

        [Fact]
        public async Task A_phone_that_is_unknown_or_is_not_a_student_gets_the_same_answer()
        {
            var teacher = await _api.SharedTeacherAsync();
            var code = await _api.NewCodeAsync(CodeKind.FreeWeek);

            var unknownPhone = await ActivateAsync(TestApi.NextPhone(), code);
            await TestApi.AssertProblemAsync(unknownPhone, HttpStatusCode.Unauthorized, "Activation.Invalid");

            // A teacher's phone exists, but a student code must never set or change a teacher's password.
            var teacherPhone = await ActivateAsync(teacher.Login.Phone, code);
            Assert.NotEqual(HttpStatusCode.OK, teacherPhone.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await _api.LoginAsync(teacher.Login.Phone, teacher.Login.Password)).StatusCode);
        }

        [Fact]
        public async Task A_student_added_by_two_teachers_must_say_which_teacher_and_gets_days_only_there()
        {
            var ahmed = await _api.CreateTeacherAsync();
            var mohamed = await _api.CreateTeacherAsync();
            var student = await NewUnactivatedStudentAsync(ahmed);
            await NewUnactivatedStudentAsync(mohamed, student.PhoneNumber);
            var code = await _api.NewCodeAsync(CodeKind.FreeWeek);

            var withoutTeacher = await ActivateAsync(student.PhoneNumber, code);
            await TestApi.AssertProblemAsync(withoutTeacher, HttpStatusCode.BadRequest, "Activation.ChooseTeacher");

            var wrongTeacher = await ActivateAsync(student.PhoneNumber, code, tenantSlug: "no-such-teacher");
            await TestApi.AssertProblemAsync(wrongTeacher, HttpStatusCode.Unauthorized, "Activation.Invalid");

            var atAhmed = await ActivateAsync(student.PhoneNumber, code, tenantSlug: ahmed.Tenant.Slug);
            Assert.Equal(HttpStatusCode.OK, atAhmed.StatusCode);

            // One login, two teachers; the free week is only at Ahmed.
            var session = await _api.LoginStudentAsync(student.PhoneNumber, "phone-A", NewPassword);
            Assert.Equal(2, session.Memberships.Count);

            async Task<SubscriptionState> StateAtAsync(int tenantId)
            {
                var choose = await TestApi.PostAsync(_api.Anonymous(), "/api/auth/refresh",
                    new RefreshRequest { RefreshToken = session.RefreshToken, DeviceId = "phone-A", TenantId = tenantId });
                session = await TestApi.ReadAsync<SessionDto>(choose);
                return (await TestApi.SubscriptionStatusAsync(_api.WithToken(session.AccessToken))).Status;
            }

            Assert.Equal(SubscriptionState.Trial, await StateAtAsync(ahmed.Tenant.Id));
            Assert.Equal(SubscriptionState.NotActivated, await StateAtAsync(mohamed.Tenant.Id));
        }

        [Fact]
        public async Task The_activation_answer_never_contains_secrets()
        {
            var teacher = await _api.SharedTeacherAsync();
            var student = await NewUnactivatedStudentAsync(teacher);

            var response = await ActivateAsync(student.PhoneNumber, await _api.NewCodeAsync(CodeKind.FreeWeek));
            var body = await response.Content.ReadAsStringAsync();

            Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("connectionString", body, StringComparison.OrdinalIgnoreCase);
        }
    }
}
