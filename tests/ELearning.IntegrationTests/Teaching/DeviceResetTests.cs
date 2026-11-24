using ELearning.IntegrationTests.Infrastructure;
using ELearning.Shared.DTOs;
using ELearning.Shared.DTOs.Auth;
using ELearning.Shared.DTOs.Teaching;
using System.Net;

namespace ELearning.IntegrationTests.Teaching
{
    // A student account works on one device. Staff can unlock it for a new device, at most twice per term.
    [Collection(ApiCollection.Name)]
    public class DeviceResetTests
    {
        private readonly ApiFixture _fixture;
        private readonly TestApi _api;

        public DeviceResetTests(ApiFixture fixture)
        {
            _fixture = fixture;
            _api = new TestApi(fixture);
        }

        // A teacher with one student who has a password (so the student can log in).
        private async Task<(CreatedTeacher Teacher, HttpClient TeacherClient, StudentDto Student)> TeacherWithStudentAsync()
        {
            var teacher = await _api.CreateTeacherAsync();
            var teacherClient = _api.TeacherClient(teacher);
            var group = await _api.CreateGroupAsync(teacherClient);
            var student = await _api.CreateStudentAsync(teacherClient, group.Id);
            await _api.SetPasswordAsync(student.PhoneNumber);
            return (teacher, teacherClient, student);
        }

        private static Task<HttpResponseMessage> ResetAsync(HttpClient staffClient, int studentId)
        {
            return staffClient.PostAsync($"/api/students/{studentId}/device-reset", null);
        }

        [Fact]
        public async Task After_a_reset_the_student_can_log_in_from_a_new_phone_and_the_old_phone_stops()
        {
            await _api.EnsureCurrentTermAsync();
            var (_, teacherClient, student) = await TeacherWithStudentAsync();

            var oldPhoneSession = await _api.LoginStudentAsync(student.PhoneNumber, "old-phone");
            var newPhoneBefore = await _api.LoginAsync(student.PhoneNumber, TestApi.DefaultPassword, "new-phone");
            await TestApi.AssertProblemAsync(newPhoneBefore, HttpStatusCode.Forbidden, "Auth.DeviceMismatch");

            var reset = await ResetAsync(teacherClient, student.Id);
            var result = await TestApi.ReadAsync<DeviceResetDto>(reset);
            Assert.Equal(1, result.UsedInCurrentTerm);
            Assert.Equal(2, result.Limit);

            // The new phone becomes the linked device.
            await _api.LoginStudentAsync(student.PhoneNumber, "new-phone");

            // The old phone can no longer log in, and its saved session is cancelled.
            var oldPhoneLogin = await _api.LoginAsync(student.PhoneNumber, TestApi.DefaultPassword, "old-phone");
            await TestApi.AssertProblemAsync(oldPhoneLogin, HttpStatusCode.Forbidden, "Auth.DeviceMismatch");

            var oldPhoneRefresh = await TestApi.PostAsync(_api.Anonymous(), "/api/auth/refresh",
                new RefreshRequest { RefreshToken = oldPhoneSession.RefreshToken, DeviceId = "old-phone" });
            Assert.Equal(HttpStatusCode.Unauthorized, oldPhoneRefresh.StatusCode);
        }

        [Fact]
        public async Task The_third_reset_in_one_term_is_refused()
        {
            await _api.EnsureCurrentTermAsync();
            var (_, teacherClient, student) = await TeacherWithStudentAsync();

            await _api.LoginStudentAsync(student.PhoneNumber, "phone-1");
            Assert.Equal(HttpStatusCode.OK, (await ResetAsync(teacherClient, student.Id)).StatusCode);

            await _api.LoginStudentAsync(student.PhoneNumber, "phone-2");
            var second = await TestApi.ReadAsync<DeviceResetDto>(await ResetAsync(teacherClient, student.Id));
            Assert.Equal(2, second.UsedInCurrentTerm);

            await _api.LoginStudentAsync(student.PhoneNumber, "phone-3");
            var third = await ResetAsync(teacherClient, student.Id);
            await TestApi.AssertProblemAsync(third, HttpStatusCode.Conflict, "Device.LimitReached");

            // The third phone stays the linked one.
            await _api.LoginStudentAsync(student.PhoneNumber, "phone-3");
        }

        [Fact]
        public async Task A_reset_is_not_used_up_when_no_device_is_linked()
        {
            await _api.EnsureCurrentTermAsync();
            var (_, teacherClient, student) = await TeacherWithStudentAsync();

            // The student never logged in, so there is nothing to unlock.
            var first = await ResetAsync(teacherClient, student.Id);
            var again = await ResetAsync(teacherClient, student.Id);
            await TestApi.AssertProblemAsync(first, HttpStatusCode.Conflict, "Device.NotLinked");
            await TestApi.AssertProblemAsync(again, HttpStatusCode.Conflict, "Device.NotLinked");

            // Both real resets are still available.
            await _api.LoginStudentAsync(student.PhoneNumber, "phone-1");
            var real = await TestApi.ReadAsync<DeviceResetDto>(await ResetAsync(teacherClient, student.Id));
            Assert.Equal(1, real.UsedInCurrentTerm);
        }

        [Fact]
        public async Task A_reset_needs_a_school_term_for_todays_date()
        {
            var (_, teacherClient, student) = await TeacherWithStudentAsync();
            await _api.LoginStudentAsync(student.PhoneNumber, "phone-1");

            try
            {
                // Twenty years from now no term has been added.
                _fixture.Clock.Offset = TimeSpan.FromDays(365 * 20);

                // The teacher's token is checked with the real clock, so it is still accepted.
                var response = await ResetAsync(teacherClient, student.Id);
                await TestApi.AssertProblemAsync(response, HttpStatusCode.Conflict, "Device.NoCurrentTerm");
            }
            finally
            {
                _fixture.Clock.Offset = TimeSpan.Zero;
            }
        }

        [Fact]
        public async Task An_assistant_can_reset_a_device_and_the_teacher_sees_it_in_the_activity_log()
        {
            await _api.EnsureCurrentTermAsync();
            var (teacher, teacherClient, student) = await TeacherWithStudentAsync();
            var (_, session, assistant) = await _api.CreateAssistantAsync(teacher);
            await _api.LoginStudentAsync(student.PhoneNumber, "phone-1");

            var reset = await ResetAsync(_api.WithToken(session.AccessToken), student.Id);
            Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

            var logResponse = await teacherClient.GetAsync("/api/audit-logs?pageSize=100");
            var log = (await TestApi.ReadAsync<PaginatedResult<AuditLogDto>>(logResponse)).Data;
            var row = Assert.Single(log, entry => entry.Action == "DeviceReset");
            Assert.Equal(assistant.Id, row.UserId);
            Assert.Equal("Student", row.EntityName);
            Assert.Equal(student.Id.ToString(), row.EntityId);
        }

        [Fact]
        public async Task Resets_at_one_teacher_count_for_the_same_student_at_another_teacher()
        {
            await _api.EnsureCurrentTermAsync();
            var (_, firstTeacherClient, student) = await TeacherWithStudentAsync();

            // The same person also studies with a second teacher. They have ONE account and ONE device.
            var secondTeacher = await _api.CreateTeacherAsync();
            var secondTeacherClient = _api.TeacherClient(secondTeacher);
            var secondGroup = await _api.CreateGroupAsync(secondTeacherClient);
            var sameStudentAtSecond = await _api.CreateStudentAsync(secondTeacherClient, secondGroup.Id, student.PhoneNumber);

            await _api.LoginStudentAsync(student.PhoneNumber, "phone-1");
            Assert.Equal(HttpStatusCode.OK, (await ResetAsync(firstTeacherClient, student.Id)).StatusCode);

            await _api.LoginStudentAsync(student.PhoneNumber, "phone-2");
            Assert.Equal(HttpStatusCode.OK, (await ResetAsync(secondTeacherClient, sameStudentAtSecond.Id)).StatusCode);

            // Two resets were used in total, so neither teacher can do a third.
            await _api.LoginStudentAsync(student.PhoneNumber, "phone-3");
            var third = await ResetAsync(firstTeacherClient, student.Id);
            await TestApi.AssertProblemAsync(third, HttpStatusCode.Conflict, "Device.LimitReached");
        }
    }
}
