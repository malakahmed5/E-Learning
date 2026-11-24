using ELearning.IntegrationTests.Infrastructure;
using ELearning.Shared.DTOs;
using ELearning.Shared.DTOs.Teaching;
using ELearningHub.Core.Entities;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;

namespace ELearning.IntegrationTests.Teaching
{
    // Adding, finding, editing and suspending students.
    [Collection(ApiCollection.Name)]
    public class StudentManagementTests
    {
        private readonly TestApi _api;

        public StudentManagementTests(ApiFixture fixture)
        {
            _api = new TestApi(fixture);
        }

        private static async Task<PaginatedResult<StudentDto>> ListAsync(HttpClient client, string query = "")
        {
            var response = await client.GetAsync("/api/students" + query);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await TestApi.ReadAsync<PaginatedResult<StudentDto>>(response);
        }

        private static UpdateStudentRequest UpdateFrom(StudentDto student)
        {
            return new UpdateStudentRequest
            {
                FullName = student.FullName,
                GroupId = student.GroupId,
                ParentName = student.ParentName,
                ParentConsent = student.ParentConsent,
                HideFromHonorBoard = student.HideFromHonorBoard,
            };
        }

        [Fact]
        public async Task Adding_a_student_creates_the_student_the_login_account_and_the_link()
        {
            var teacher = await _api.CreateTeacherAsync();
            var client = _api.TeacherClient(teacher);
            var group = await _api.CreateGroupAsync(client, "First prep A", gradeLevel: 1);

            var student = await _api.CreateStudentAsync(client, group.Id, fullName: "Omar Khaled");

            Assert.Equal("Omar Khaled", student.FullName);
            Assert.Equal("First prep A", student.GroupName);
            Assert.Equal(1, student.GradeLevel);
            Assert.True(student.ParentConsent);
            Assert.True(student.IsActive);
            Assert.False(student.IsActivated);

            // The hub now has one account for this phone, linked to this teacher as a student.
            var membership = await _api.ReadHubAsync(hub => hub.Memberships
                .Include(m => m.Account)
                .SingleAsync(m => m.Account.PhoneNumber == student.PhoneNumber));
            Assert.Equal(teacher.Tenant.Id, membership.TenantId);
            Assert.Equal(MembershipRole.Student, membership.Role);
            Assert.Null(membership.Account.PasswordHash);

            // The group now counts one student.
            var groupAfter = await TestApi.ReadAsync<GroupDto>(await client.GetAsync($"/api/groups/{group.Id}"));
            Assert.Equal(1, groupAfter.StudentCount);
        }

        [Fact]
        public async Task A_new_student_cannot_log_in_before_activating_the_account()
        {
            var teacher = await _api.SharedTeacherAsync();
            var client = _api.TeacherClient(teacher);
            var group = await _api.CreateGroupAsync(client);
            var student = await _api.CreateStudentAsync(client, group.Id);

            // No password exists yet, so any password is refused.
            var login = await _api.LoginAsync(student.PhoneNumber, TestApi.DefaultPassword, "phone-A");

            await TestApi.AssertProblemAsync(login, HttpStatusCode.Unauthorized, "Auth.InvalidCredentials");
        }

        [Fact]
        public async Task The_same_phone_cannot_be_added_twice_at_one_teacher()
        {
            var teacher = await _api.SharedTeacherAsync();
            var client = _api.TeacherClient(teacher);
            var group = await _api.CreateGroupAsync(client);
            var student = await _api.CreateStudentAsync(client, group.Id);

            var again = await TestApi.PostAsync(client, "/api/students", TestApi.NewStudentRequest(group.Id, student.PhoneNumber));

            await TestApi.AssertProblemAsync(again, HttpStatusCode.Conflict, "User.PhoneTaken");
        }

        [Fact]
        public async Task A_student_of_two_teachers_keeps_one_account_and_sees_both_after_login()
        {
            var ahmed = await _api.CreateTeacherAsync("Mr Ahmed");
            var mohamed = await _api.CreateTeacherAsync("Mr Mohamed");
            var ahmedGroup = await _api.CreateGroupAsync(_api.TeacherClient(ahmed));
            var mohamedGroup = await _api.CreateGroupAsync(_api.TeacherClient(mohamed));
            var phone = TestApi.NextPhone();

            await _api.CreateStudentAsync(_api.TeacherClient(ahmed), ahmedGroup.Id, phone);
            await _api.CreateStudentAsync(_api.TeacherClient(mohamed), mohamedGroup.Id, phone);

            var accountCount = await _api.ReadHubAsync(hub => hub.Accounts.CountAsync(a => a.PhoneNumber == phone));
            Assert.Equal(1, accountCount);

            await _api.SetPasswordAsync(phone);
            var session = await _api.LoginStudentAsync(phone, "phone-A");
            Assert.Equal(2, session.Memberships.Count);
            Assert.Contains(session.Memberships, m => m.TenantName == "Mr Ahmed");
            Assert.Contains(session.Memberships, m => m.TenantName == "Mr Mohamed");
        }

        [Fact]
        public async Task Students_cannot_be_added_to_missing_or_turned_off_groups()
        {
            var teacher = await _api.SharedTeacherAsync();
            var client = _api.TeacherClient(teacher);
            var group = await _api.CreateGroupAsync(client);
            await client.PutAsJsonAsync($"/api/groups/{group.Id}/status", new SetActiveRequest { IsActive = false });

            var missingGroup = await TestApi.PostAsync(client, "/api/students", TestApi.NewStudentRequest(999_999));
            var inactiveGroup = await TestApi.PostAsync(client, "/api/students", TestApi.NewStudentRequest(group.Id));

            await TestApi.AssertProblemAsync(missingGroup, HttpStatusCode.NotFound, "Group.NotFound");
            await TestApi.AssertProblemAsync(inactiveGroup, HttpStatusCode.Conflict, "Group.Inactive");
        }

        [Theory]
        [InlineData("", "01012345678", "Parent", "01112345678")]          // no name
        [InlineData("Ali", "12345", "Parent", "01112345678")]             // bad student phone
        [InlineData("Ali", "01012345678", "Parent", "not-a-phone")]       // bad parent phone
        [InlineData("Ali", "01012345678", "", "01112345678")]             // no parent name
        public async Task Invalid_student_data_is_refused(string name, string phone, string parentName, string parentPhone)
        {
            var teacher = await _api.SharedTeacherAsync();
            var client = _api.TeacherClient(teacher);
            var group = await _api.CreateGroupAsync(client);

            var response = await TestApi.PostAsync(client, "/api/students", new CreateStudentRequest
            {
                FullName = name,
                PhoneNumber = phone,
                GroupId = group.Id,
                ParentName = parentName,
                ParentWhatsApp = parentPhone,
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Search_filters_and_pages_work_together()
        {
            var teacher = await _api.CreateTeacherAsync();
            var client = _api.TeacherClient(teacher);
            var firstPrep = await _api.CreateGroupAsync(client, "First prep", gradeLevel: 1);
            var thirdSecondary = await _api.CreateGroupAsync(client, "Third secondary", gradeLevel: 6);

            var mona = await _api.CreateStudentAsync(client, firstPrep.Id, fullName: "Mona Adel");
            await _api.CreateStudentAsync(client, firstPrep.Id, fullName: "Mostafa Samir");
            await _api.CreateStudentAsync(client, firstPrep.Id, fullName: "Nour Hany");
            await _api.CreateStudentAsync(client, thirdSecondary.Id, fullName: "Youssef Tarek");
            await _api.CreateStudentAsync(client, thirdSecondary.Id, fullName: "Zeinab Fathy");

            // Search by part of the name, with different letter case.
            var byName = await ListAsync(client, "?search=MONA");
            Assert.Equal("Mona Adel", Assert.Single(byName.Data).FullName);

            // Search by part of the phone number.
            var byPhone = await ListAsync(client, "?search=" + mona.PhoneNumber.Substring(3));
            Assert.Equal(mona.Id, Assert.Single(byPhone.Data).Id);

            // Filters.
            Assert.Equal(3, (await ListAsync(client, $"?groupId={firstPrep.Id}")).Count);
            Assert.Equal(2, (await ListAsync(client, "?gradeLevel=6")).Count);
            Assert.Equal(0, (await ListAsync(client, "?gradeLevel=2")).Count);

            // Pages: 5 students, 2 per page, sorted by name.
            var firstPage = await ListAsync(client, "?pageSize=2&pageIndex=1");
            var lastPage = await ListAsync(client, "?pageSize=2&pageIndex=3");
            Assert.Equal(5, firstPage.Count);
            Assert.Equal(new[] { "Mona Adel", "Mostafa Samir" }, firstPage.Data.Select(s => s.FullName));
            Assert.Equal("Zeinab Fathy", Assert.Single(lastPage.Data).FullName);

            // Text that looks like a search pattern or SQL is treated as plain text and finds nothing.
            Assert.Equal(0, (await ListAsync(client, "?search=%25")).Count);
            Assert.Equal(0, (await ListAsync(client, "?search=' OR '1'='1")).Count);
        }

        [Theory]
        [InlineData("?pageSize=1000")]
        [InlineData("?pageSize=0")]
        [InlineData("?pageIndex=0")]
        [InlineData("?gradeLevel=9")]
        public async Task Unreasonable_list_requests_are_refused(string query)
        {
            var teacher = await _api.SharedTeacherAsync();

            var response = await _api.TeacherClient(teacher).GetAsync("/api/students" + query);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task A_student_moves_only_between_groups_of_the_same_grade()
        {
            var teacher = await _api.SharedTeacherAsync();
            var client = _api.TeacherClient(teacher);
            var groupA = await _api.CreateGroupAsync(client, gradeLevel: 2);
            var groupB = await _api.CreateGroupAsync(client, gradeLevel: 2);
            var otherGrade = await _api.CreateGroupAsync(client, gradeLevel: 5);
            var student = await _api.CreateStudentAsync(client, groupA.Id);

            var sameGradeRequest = UpdateFrom(student);
            sameGradeRequest.GroupId = groupB.Id;
            var sameGrade = await client.PutAsJsonAsync($"/api/students/{student.Id}", sameGradeRequest);
            Assert.Equal(groupB.Id, (await TestApi.ReadAsync<StudentDto>(sameGrade)).GroupId);

            var otherGradeRequest = UpdateFrom(student);
            otherGradeRequest.GroupId = otherGrade.Id;
            var differentGrade = await client.PutAsJsonAsync($"/api/students/{student.Id}", otherGradeRequest);
            await TestApi.AssertProblemAsync(differentGrade, HttpStatusCode.BadRequest, "Student.GroupGradeMismatch");
        }

        [Fact]
        public async Task Editing_a_student_updates_name_parent_and_consent()
        {
            var teacher = await _api.SharedTeacherAsync();
            var client = _api.TeacherClient(teacher);
            var group = await _api.CreateGroupAsync(client);
            var student = await _api.CreateStudentAsync(client, group.Id);

            var request = UpdateFrom(student);
            request.FullName = "New Name";
            request.ParentName = "New Parent";
            request.ParentWhatsApp = "01211112222";
            request.ParentConsent = false;
            request.HideFromHonorBoard = true;
            var updated = await TestApi.ReadAsync<StudentDto>(await client.PutAsJsonAsync($"/api/students/{student.Id}", request));

            Assert.Equal("New Name", updated.FullName);
            Assert.Equal("New Parent", updated.ParentName);
            Assert.Equal("01211112222", updated.ParentWhatsApp);
            Assert.False(updated.ParentConsent);
            Assert.True(updated.HideFromHonorBoard);
        }

        [Fact]
        public async Task Suspending_a_student_blocks_them_at_once_and_restoring_gives_access_back()
        {
            var teacher = await _api.CreateTeacherAsync();
            var client = _api.TeacherClient(teacher);
            var group = await _api.CreateGroupAsync(client);
            var student = await _api.CreateStudentAsync(client, group.Id);
            await _api.SetPasswordAsync(student.PhoneNumber);
            var session = await _api.LoginStudentAsync(student.PhoneNumber, "phone-A");
            var studentClient = _api.WithToken(session.AccessToken);
            Assert.Equal(HttpStatusCode.OK, (await studentClient.GetAsync("/api/profile")).StatusCode);

            var suspend = await client.PutAsJsonAsync($"/api/students/{student.Id}/status", new SetActiveRequest { IsActive = false });
            Assert.Equal(HttpStatusCode.NoContent, suspend.StatusCode);

            // The student's token has not expired, but it stops working now.
            Assert.Equal(HttpStatusCode.Forbidden, (await studentClient.GetAsync("/api/profile")).StatusCode);
            Assert.False((await TestApi.ReadAsync<StudentDto>(await client.GetAsync($"/api/students/{student.Id}"))).IsActive);

            await client.PutAsJsonAsync($"/api/students/{student.Id}/status", new SetActiveRequest { IsActive = true });
            Assert.Equal(HttpStatusCode.OK, (await studentClient.GetAsync("/api/profile")).StatusCode);
        }

        [Fact]
        public async Task A_teacher_cannot_read_change_or_suspend_another_teachers_student()
        {
            var ahmed = await _api.CreateTeacherAsync();
            var mohamed = await _api.CreateTeacherAsync();
            var ahmedClient = _api.TeacherClient(ahmed);
            var mohamedClient = _api.TeacherClient(mohamed);
            var ahmedGroup = await _api.CreateGroupAsync(ahmedClient);
            var ahmedStudent = await _api.CreateStudentAsync(ahmedClient, ahmedGroup.Id, fullName: "Belongs to Ahmed");

            // Mohamed tries Ahmed's student id. His own database has no such student.
            var read = await mohamedClient.GetAsync($"/api/students/{ahmedStudent.Id}");
            var change = await mohamedClient.PutAsJsonAsync($"/api/students/{ahmedStudent.Id}", UpdateFrom(ahmedStudent));
            var suspend = await mohamedClient.PutAsJsonAsync($"/api/students/{ahmedStudent.Id}/status", new SetActiveRequest { IsActive = false });
            var unlock = await mohamedClient.PostAsync($"/api/students/{ahmedStudent.Id}/device-reset", null);
            var list = await ListAsync(mohamedClient);

            Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, change.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, suspend.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, unlock.StatusCode);
            Assert.Equal(0, list.Count);

            // Ahmed's student is untouched.
            var stillThere = await TestApi.ReadAsync<StudentDto>(await ahmedClient.GetAsync($"/api/students/{ahmedStudent.Id}"));
            Assert.Equal("Belongs to Ahmed", stillThere.FullName);
            Assert.True(stillThere.IsActive);
        }

        [Fact]
        public async Task A_student_cannot_use_the_staff_endpoints()
        {
            var teacher = await _api.CreateTeacherAsync();
            var client = _api.TeacherClient(teacher);
            var group = await _api.CreateGroupAsync(client);
            var student = await _api.CreateStudentAsync(client, group.Id);
            await _api.SetPasswordAsync(student.PhoneNumber);
            var session = await _api.LoginStudentAsync(student.PhoneNumber, "phone-A");
            var studentClient = _api.WithToken(session.AccessToken);

            var responses = new[]
            {
                await studentClient.GetAsync("/api/students"),
                await studentClient.GetAsync($"/api/students/{student.Id}"),
                await studentClient.GetAsync("/api/groups"),
                await studentClient.GetAsync("/api/centers"),
                await studentClient.GetAsync("/api/assistants"),
                await studentClient.GetAsync("/api/audit-logs"),
                await TestApi.PostAsync(studentClient, "/api/students", TestApi.NewStudentRequest(group.Id)),
                await studentClient.PutAsJsonAsync($"/api/students/{student.Id}/status", new SetActiveRequest { IsActive = true }),
                await studentClient.PostAsync($"/api/students/{student.Id}/device-reset", null),
                await studentClient.DeleteAsync($"/api/groups/{group.Id}"),
            };

            Assert.All(responses, response => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode));
        }
    }
}
