using ELearning.Core.Contracts;
using ELearning.Core.Entities.Identity;
using ELearning.IntegrationTests.Infrastructure;
using ELearning.Shared.DTOs.Admin;
using ELearning.Shared.DTOs.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace ELearning.IntegrationTests.Admin
{
    // The platform admin registers teachers; the teacher sets a password with the invite code.
    [Collection(ApiCollection.Name)]
    public class TeacherRegistrationTests
    {
        private readonly ApiFixture _fixture;
        private readonly TestApi _api;

        public TeacherRegistrationTests(ApiFixture fixture)
        {
            _fixture = fixture;
            _api = new TestApi(fixture);
        }

        private static CreateTenantRequest NewTeacherRequest(string? slug = null, string? phone = null)
        {
            slug ??= TestApi.NextSlug();
            return new CreateTenantRequest
            {
                Slug = slug,
                DisplayName = "Teacher " + slug,
                TeacherFullName = "Mr " + slug,
                TeacherPhoneNumber = phone ?? TestApi.NextPhone(),
            };
        }

        [Fact]
        public async Task Registering_a_teacher_creates_a_database_with_the_teacher_inside()
        {
            var admin = await _api.AdminClientAsync();
            var request = NewTeacherRequest();

            var response = await TestApi.PostAsync(admin, "/api/admin/tenants", request);
            var created = await TestApi.ReadAsync<CreateTenantResponse>(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(created.Tenant.IsActive);
            Assert.NotNull(created.Invite);

            // Open the new teacher database directly and look inside.
            var connectionString = await _api.ReadHubAsync(hub =>
                hub.Tenants.Where(t => t.Id == created.Tenant.Id).Select(t => t.ConnectionString).SingleAsync());
            var tenantDatabases = _fixture.Services.GetRequiredService<ITenantDatabaseManager>();
            await using var teacherDatabase = tenantDatabases.Open(connectionString);

            var users = await teacherDatabase.Users.ToListAsync();
            var onlyUser = Assert.Single(users);
            Assert.Equal(UserRole.Teacher, onlyUser.Role);
            Assert.Equal(request.TeacherPhoneNumber, onlyUser.PhoneNumber);
        }

        [Fact]
        public async Task Full_journey_admin_registers_teacher_who_then_logs_in()
        {
            var admin = await _api.AdminClientAsync();
            var request = NewTeacherRequest();
            var created = await TestApi.ReadAsync<CreateTenantResponse>(
                await TestApi.PostAsync(admin, "/api/admin/tenants", request));

            // Before accepting the invite there is no password, so login fails.
            var tooEarly = await _api.LoginAsync(request.TeacherPhoneNumber, TestApi.DefaultPassword);
            Assert.Equal(HttpStatusCode.Unauthorized, tooEarly.StatusCode);

            var accept = await TestApi.PostAsync(_api.Anonymous(), "/api/auth/accept-invite", new AcceptInviteRequest
            {
                PhoneNumber = request.TeacherPhoneNumber,
                InviteCode = created.Invite!.Code,
                Password = TestApi.DefaultPassword,
            });
            Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);

            var (session, _) = await _api.SetUpTwoFactorAndLoginAsync(request.TeacherPhoneNumber, TestApi.DefaultPassword);
            Assert.Equal("Teacher", session.ActiveMembership!.Role);

            var profile = await _api.WithToken(session.AccessToken).GetAsync("/api/profile");
            Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
            Assert.Equal("Teacher", (await TestApi.ReadAsync<TenantProfileDto>(profile)).Role);
        }

        [Fact]
        public async Task Invite_code_works_only_once()
        {
            var admin = await _api.AdminClientAsync();
            var request = NewTeacherRequest();
            var created = await TestApi.ReadAsync<CreateTenantResponse>(
                await TestApi.PostAsync(admin, "/api/admin/tenants", request));
            var acceptRequest = new AcceptInviteRequest
            {
                PhoneNumber = request.TeacherPhoneNumber,
                InviteCode = created.Invite!.Code,
                Password = TestApi.DefaultPassword,
            };

            var first = await TestApi.PostAsync(_api.Anonymous(), "/api/auth/accept-invite", acceptRequest);
            var second = await TestApi.PostAsync(_api.Anonymous(), "/api/auth/accept-invite", acceptRequest);

            Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
            await TestApi.AssertProblemAsync(second, HttpStatusCode.Unauthorized, "Auth.InvalidInvite");
        }

        [Fact]
        public async Task Wrong_or_expired_invite_code_is_refused()
        {
            var admin = await _api.AdminClientAsync();
            var request = NewTeacherRequest();
            var created = await TestApi.ReadAsync<CreateTenantResponse>(
                await TestApi.PostAsync(admin, "/api/admin/tenants", request));

            var wrongCode = await TestApi.PostAsync(_api.Anonymous(), "/api/auth/accept-invite", new AcceptInviteRequest
            {
                PhoneNumber = request.TeacherPhoneNumber,
                InviteCode = "AAAA-BBBB-CCCC",
                Password = TestApi.DefaultPassword,
            });
            await TestApi.AssertProblemAsync(wrongCode, HttpStatusCode.Unauthorized, "Auth.InvalidInvite");

            try
            {
                // The invite is valid for 7 days.
                _fixture.Clock.Offset = TimeSpan.FromDays(8);
                var expired = await TestApi.PostAsync(_api.Anonymous(), "/api/auth/accept-invite", new AcceptInviteRequest
                {
                    PhoneNumber = request.TeacherPhoneNumber,
                    InviteCode = created.Invite!.Code,
                    Password = TestApi.DefaultPassword,
                });
                await TestApi.AssertProblemAsync(expired, HttpStatusCode.Unauthorized, "Auth.InvalidInvite");
            }
            finally
            {
                _fixture.Clock.Offset = TimeSpan.Zero;
            }
        }

        [Theory]
        [InlineData("short")]            // 5 characters: caught by the request rule (minimum 8)
        [InlineData("onlyletters")]      // no digit
        [InlineData("1234567890")]       // no letter
        public async Task Weak_password_is_refused(string weakPassword)
        {
            // The password is checked before anything else, so no real invite is needed for this test.
            var response = await TestApi.PostAsync(_api.Anonymous(), "/api/auth/accept-invite", new AcceptInviteRequest
            {
                PhoneNumber = TestApi.NextPhone(),
                InviteCode = "AAAA-BBBB-CCCC",
                Password = weakPassword,
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task The_invite_code_itself_is_not_stored()
        {
            var admin = await _api.AdminClientAsync();
            var request = NewTeacherRequest();
            var created = await TestApi.ReadAsync<CreateTenantResponse>(
                await TestApi.PostAsync(admin, "/api/admin/tenants", request));

            var storedHash = await _api.ReadHubAsync(hub =>
                hub.Accounts.Where(a => a.PhoneNumber == request.TeacherPhoneNumber).Select(a => a.InviteCodeHash).SingleAsync());

            var codeWithoutDashes = created.Invite!.Code.Replace("-", "");
            Assert.NotNull(storedHash);
            Assert.NotEqual(codeWithoutDashes, storedHash);
        }

        [Fact]
        public async Task Two_teachers_cannot_have_the_same_link_name()
        {
            var admin = await _api.AdminClientAsync();
            var slug = TestApi.NextSlug();
            await TestApi.PostAsync(admin, "/api/admin/tenants", NewTeacherRequest(slug));

            var duplicate = await TestApi.PostAsync(admin, "/api/admin/tenants", NewTeacherRequest(slug));

            await TestApi.AssertProblemAsync(duplicate, HttpStatusCode.Conflict, "Tenant.SlugTaken");
        }

        [Theory]
        [InlineData("admin")]                 // reserved for the platform
        [InlineData("Has-Capitals")]
        [InlineData("has spaces")]
        [InlineData("ab")]                    // too short
        [InlineData("bad'; drop database--")]
        [InlineData("../etc")]
        public async Task Invalid_link_names_are_refused(string badSlug)
        {
            var admin = await _api.AdminClientAsync();

            var response = await TestApi.PostAsync(admin, "/api/admin/tenants", NewTeacherRequest(badSlug));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Invalid_teacher_phone_is_refused()
        {
            var admin = await _api.AdminClientAsync();

            var response = await TestApi.PostAsync(admin, "/api/admin/tenants", NewTeacherRequest(phone: "12345"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task A_new_invite_lets_the_teacher_set_a_new_password_and_ends_old_sessions()
        {
            var teacher = await _api.CreateTeacherAsync();
            var oldSession = await _api.LoginStaffAsync(teacher.Login);
            var admin = await _api.AdminClientAsync();

            var inviteResponse = await admin.PostAsync($"/api/admin/tenants/{teacher.Tenant.Id}/teacher-invite", null);
            var invite = await TestApi.ReadAsync<InviteDto>(inviteResponse);

            var accept = await TestApi.PostAsync(_api.Anonymous(), "/api/auth/accept-invite", new AcceptInviteRequest
            {
                PhoneNumber = teacher.Login.Phone,
                InviteCode = invite.Code,
                Password = "BrandNewPass9",
            });
            Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);

            // The old password no longer works, the new one does.
            var oldPassword = await _api.LoginAsync(teacher.Login.Phone, teacher.Login.Password);
            var newPassword = await _api.LoginAsync(teacher.Login.Phone, "BrandNewPass9");
            Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
            Assert.Equal(HttpStatusCode.OK, newPassword.StatusCode);

            // Sessions opened with the old password are closed.
            var oldRefresh = await TestApi.PostAsync(_api.Anonymous(), "/api/auth/refresh",
                new RefreshRequest { RefreshToken = oldSession.RefreshToken });
            Assert.Equal(HttpStatusCode.Unauthorized, oldRefresh.StatusCode);
        }

        [Fact]
        public async Task Teacher_public_page_shows_name_without_login_and_hides_unknown_or_inactive_teachers()
        {
            var teacher = await _api.CreateTeacherAsync("Mr Public");

            var found = await _api.Anonymous().GetAsync($"/api/auth/tenants/{teacher.Tenant.Slug}");
            var branding = await TestApi.ReadAsync<TenantBrandingDto>(found);
            Assert.Equal("Mr Public", branding.DisplayName);

            var unknown = await _api.Anonymous().GetAsync("/api/auth/tenants/nobody-here");
            Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

            var admin = await _api.AdminClientAsync();
            await admin.PutAsJsonAsync($"/api/admin/tenants/{teacher.Tenant.Id}/status", new { isActive = false });
            var inactive = await _api.Anonymous().GetAsync($"/api/auth/tenants/{teacher.Tenant.Slug}");
            Assert.Equal(HttpStatusCode.NotFound, inactive.StatusCode);
        }

        [Fact]
        public async Task Changing_status_of_an_unknown_teacher_gives_not_found()
        {
            var admin = await _api.AdminClientAsync();

            var response = await admin.PutAsJsonAsync("/api/admin/tenants/999999/status", new { isActive = false });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
