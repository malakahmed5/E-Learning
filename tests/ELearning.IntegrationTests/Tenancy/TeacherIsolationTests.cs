using ELearning.IntegrationTests.Infrastructure;
using ELearning.Shared.DTOs.Auth;
using ELearningHub.Core.Entities;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;

namespace ELearning.IntegrationTests.Tenancy
{
    // The most important rule of the platform: each teacher's data is separate.
    // These tests check choosing a teacher, switching, and that access stops when it should.
    [Collection(ApiCollection.Name)]
    public class TeacherIsolationTests
    {
        private readonly TestApi _api;

        public TeacherIsolationTests(ApiFixture fixture)
        {
            _api = new TestApi(fixture);
        }

        private Task<HttpResponseMessage> ChooseTeacherAsync(SessionDto session, int tenantId)
        {
            return TestApi.PostAsync(_api.Anonymous(), "/api/auth/refresh", new RefreshRequest
            {
                RefreshToken = session.RefreshToken,
                DeviceId = "phone-A",
                TenantId = tenantId,
            });
        }

        private async Task<TenantProfileDto> GetProfileAsync(string accessToken)
        {
            var response = await _api.WithToken(accessToken).GetAsync("/api/profile");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await TestApi.ReadAsync<TenantProfileDto>(response);
        }

        [Fact]
        public async Task Student_with_two_teachers_logs_in_once_and_chooses_between_them()
        {
            var ahmed = await _api.CreateTeacherAsync("Mr Ahmed");
            var mohamed = await _api.CreateTeacherAsync("Mr Mohamed");

            // The same person is a student at both teachers. The name is different in each
            // database on purpose, so the test can prove which database answered.
            var phone = await _api.AddMemberAsync(ahmed.Tenant.Id, MembershipRole.Student, fullName: "Student at Ahmed");
            await _api.AddMemberAsync(mohamed.Tenant.Id, MembershipRole.Student, phone, fullName: "Student at Mohamed");

            var session = await _api.LoginStudentAsync(phone, "phone-A");

            // One login shows both teachers and waits for the student to choose.
            Assert.Equal(2, session.Memberships.Count);
            Assert.Null(session.ActiveMembership);

            var atAhmed = await TestApi.ReadAsync<SessionDto>(await ChooseTeacherAsync(session, ahmed.Tenant.Id));
            Assert.Equal(ahmed.Tenant.Id, atAhmed.ActiveMembership!.TenantId);
            Assert.Equal("Student at Ahmed", (await GetProfileAsync(atAhmed.AccessToken)).FullName);

            // Switching teacher needs no password.
            var atMohamed = await TestApi.ReadAsync<SessionDto>(await ChooseTeacherAsync(atAhmed, mohamed.Tenant.Id));
            Assert.Equal(mohamed.Tenant.Id, atMohamed.ActiveMembership!.TenantId);
            Assert.Equal("Student at Mohamed", (await GetProfileAsync(atMohamed.AccessToken)).FullName);
        }

        [Fact]
        public async Task Student_cannot_choose_a_teacher_they_do_not_belong_to()
        {
            var myTeacher = await _api.CreateTeacherAsync();
            var otherTeacher = await _api.CreateTeacherAsync();
            var phone = await _api.AddMemberAsync(myTeacher.Tenant.Id, MembershipRole.Student);
            var session = await _api.LoginStudentAsync(phone, "phone-A");

            var response = await ChooseTeacherAsync(session, otherTeacher.Tenant.Id);

            await TestApi.AssertProblemAsync(response, HttpStatusCode.Forbidden, "Auth.TenantNotAvailable");
        }

        [Fact]
        public async Task Choosing_a_teacher_that_does_not_exist_is_refused()
        {
            var teacher = await _api.CreateTeacherAsync();
            var phone = await _api.AddMemberAsync(teacher.Tenant.Id, MembershipRole.Student);
            var session = await _api.LoginStudentAsync(phone, "phone-A");

            var response = await ChooseTeacherAsync(session, tenantId: 999_999);

            await TestApi.AssertProblemAsync(response, HttpStatusCode.Forbidden, "Auth.TenantNotAvailable");
        }

        [Fact]
        public async Task Each_teacher_only_sees_people_of_their_own_database()
        {
            var ahmed = await _api.CreateTeacherAsync();
            var mohamed = await _api.CreateTeacherAsync();

            var ahmedSession = await _api.LoginStaffAsync(ahmed.Login);
            var mohamedSession = await _api.LoginStaffAsync(mohamed.Login);

            var ahmedProfile = await GetProfileAsync(ahmedSession.AccessToken);
            var mohamedProfile = await GetProfileAsync(mohamedSession.AccessToken);

            // Both are user number 1 in their own database, and each one gets their own row.
            Assert.Equal(ahmed.Login.Phone, ahmedProfile.PhoneNumber);
            Assert.Equal(mohamed.Login.Phone, mohamedProfile.PhoneNumber);
            Assert.Equal(ahmedProfile.UserId, mohamedProfile.UserId);
        }

        [Fact]
        public async Task A_session_without_a_chosen_teacher_cannot_read_teacher_data()
        {
            var admin = await _api.AdminClientAsync();

            // The admin is logged in but is not inside any teacher's platform.
            var response = await admin.GetAsync("/api/profile");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Suspending_a_member_stops_their_current_token_at_once()
        {
            var teacher = await _api.CreateTeacherAsync();
            var assistantPhone = await _api.AddMemberAsync(teacher.Tenant.Id, MembershipRole.Assistant);
            var (session, _) = await _api.SetUpTwoFactorAndLoginAsync(assistantPhone, TestApi.DefaultPassword);
            Assert.Equal("Assistant", session.ActiveMembership!.Role);
            await GetProfileAsync(session.AccessToken);

            // The teacher suspends the assistant.
            await _api.ChangeHubAsync(async hub =>
            {
                var membership = await hub.Memberships.SingleAsync(m =>
                    m.TenantId == teacher.Tenant.Id && m.Account.PhoneNumber == assistantPhone);
                membership.IsActive = false;
            });

            // The assistant's token has not expired, but it must stop working now.
            var response = await _api.WithToken(session.AccessToken).GetAsync("/api/profile");

            await TestApi.AssertProblemAsync(response, HttpStatusCode.Forbidden, "Auth.TenantNotAvailable");
        }

        [Fact]
        public async Task Turning_a_teacher_off_blocks_everyone_and_turning_on_restores_access()
        {
            var teacher = await _api.CreateTeacherAsync();
            var phone = await _api.AddMemberAsync(teacher.Tenant.Id, MembershipRole.Student);
            var session = await _api.LoginStudentAsync(phone, "phone-A");
            var admin = await _api.AdminClientAsync();

            var turnOff = await admin.PutAsJsonAsync($"/api/admin/tenants/{teacher.Tenant.Id}/status", new { isActive = false });
            Assert.Equal(HttpStatusCode.NoContent, turnOff.StatusCode);

            var whileOff = await _api.WithToken(session.AccessToken).GetAsync("/api/profile");
            Assert.Equal(HttpStatusCode.Forbidden, whileOff.StatusCode);

            // A new login no longer lists the teacher.
            var loginWhileOff = await _api.LoginStudentAsync(phone, "phone-A");
            Assert.Empty(loginWhileOff.Memberships);

            var turnOn = await admin.PutAsJsonAsync($"/api/admin/tenants/{teacher.Tenant.Id}/status", new { isActive = true });
            Assert.Equal(HttpStatusCode.NoContent, turnOn.StatusCode);

            var whileOn = await _api.WithToken(session.AccessToken).GetAsync("/api/profile");
            Assert.Equal(HttpStatusCode.OK, whileOn.StatusCode);
        }

        [Fact]
        public async Task Students_and_teachers_cannot_call_platform_admin_endpoints()
        {
            var teacher = await _api.CreateTeacherAsync();
            var studentPhone = await _api.AddMemberAsync(teacher.Tenant.Id, MembershipRole.Student);
            var studentSession = await _api.LoginStudentAsync(studentPhone, "phone-A");
            var teacherSession = await _api.LoginStaffAsync(teacher.Login);

            var asStudent = await _api.WithToken(studentSession.AccessToken).GetAsync("/api/admin/tenants");
            var asTeacher = await _api.WithToken(teacherSession.AccessToken).GetAsync("/api/admin/tenants");
            var asNobody = await _api.Anonymous().GetAsync("/api/admin/tenants");

            Assert.Equal(HttpStatusCode.Forbidden, asStudent.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, asTeacher.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, asNobody.StatusCode);
        }

        [Fact]
        public async Task The_list_of_teachers_never_exposes_connection_strings()
        {
            await _api.CreateTeacherAsync();
            var admin = await _api.AdminClientAsync();

            var body = await (await admin.GetAsync("/api/admin/tenants")).Content.ReadAsStringAsync();

            Assert.DoesNotContain("connectionString", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Password=", body, StringComparison.OrdinalIgnoreCase);
        }
    }
}
