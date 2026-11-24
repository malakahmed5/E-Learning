using ELearning.IntegrationTests.Infrastructure;
using ELearning.Shared.DTOs;
using ELearning.Shared.DTOs.Teaching;
using System.Net;
using System.Net.Http.Json;

namespace ELearning.IntegrationTests.Teaching
{
    // Assistants: what they may do, what is hidden from them, and the record of what they did.
    [Collection(ApiCollection.Name)]
    public class AssistantsAndActivityLogTests
    {
        private readonly TestApi _api;

        public AssistantsAndActivityLogTests(ApiFixture fixture)
        {
            _api = new TestApi(fixture);
        }

        private static async Task<List<AuditLogDto>> ReadLogAsync(HttpClient teacherClient, string query = "?pageSize=100")
        {
            var response = await teacherClient.GetAsync("/api/audit-logs" + query);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await TestApi.ReadAsync<PaginatedResult<AuditLogDto>>(response)).Data.ToList();
        }

        [Fact]
        public async Task Teacher_adds_an_assistant_who_activates_the_account_and_logs_in()
        {
            var teacher = await _api.CreateTeacherAsync();

            var (_, session, assistant) = await _api.CreateAssistantAsync(teacher);

            Assert.Equal("Assistant", session.ActiveMembership!.Role);
            Assert.Equal(teacher.Tenant.Id, session.ActiveMembership.TenantId);

            var list = await TestApi.ReadAsync<List<AssistantDto>>(await _api.TeacherClient(teacher).GetAsync("/api/assistants"));
            Assert.Equal(assistant.Id, Assert.Single(list).Id);
        }

        [Fact]
        public async Task The_same_phone_cannot_be_an_assistant_twice()
        {
            var teacher = await _api.SharedTeacherAsync();
            var client = _api.TeacherClient(teacher);
            var phone = TestApi.NextPhone();
            await TestApi.PostAsync(client, "/api/assistants", new CreateAssistantRequest { FullName = "First", PhoneNumber = phone });

            var again = await TestApi.PostAsync(client, "/api/assistants", new CreateAssistantRequest { FullName = "Second", PhoneNumber = phone });

            await TestApi.AssertProblemAsync(again, HttpStatusCode.Conflict, "User.PhoneTaken");
        }

        [Fact]
        public async Task An_assistant_does_daily_work_but_cannot_open_the_settings_areas()
        {
            var teacher = await _api.CreateTeacherAsync();
            var (_, session, assistant) = await _api.CreateAssistantAsync(teacher);
            var assistantClient = _api.WithToken(session.AccessToken);

            // Daily work is allowed.
            var group = await _api.CreateGroupAsync(assistantClient);
            await _api.CreateStudentAsync(assistantClient, group.Id);
            Assert.Equal(HttpStatusCode.OK, (await assistantClient.GetAsync("/api/students")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await assistantClient.GetAsync("/api/centers")).StatusCode);

            // Managing assistants and reading the activity log are for the teacher only.
            var listAssistants = await assistantClient.GetAsync("/api/assistants");
            var addAssistant = await TestApi.PostAsync(assistantClient, "/api/assistants",
                new CreateAssistantRequest { FullName = "Friend", PhoneNumber = TestApi.NextPhone() });
            var restoreSelf = await assistantClient.PutAsJsonAsync($"/api/assistants/{assistant.Id}/status", new SetActiveRequest { IsActive = true });
            var readLog = await assistantClient.GetAsync("/api/audit-logs");

            Assert.Equal(HttpStatusCode.Forbidden, listAssistants.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, addAssistant.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, restoreSelf.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, readLog.StatusCode);
        }

        [Fact]
        public async Task The_parents_phone_is_hidden_from_assistants_and_shown_to_the_teacher()
        {
            var teacher = await _api.CreateTeacherAsync();
            var teacherClient = _api.TeacherClient(teacher);
            var (_, session, _) = await _api.CreateAssistantAsync(teacher);
            var assistantClient = _api.WithToken(session.AccessToken);
            var group = await _api.CreateGroupAsync(teacherClient);
            var student = await _api.CreateStudentAsync(teacherClient, group.Id);

            var seenByTeacher = await TestApi.ReadAsync<StudentDto>(await teacherClient.GetAsync($"/api/students/{student.Id}"));
            var seenByAssistant = await TestApi.ReadAsync<StudentDto>(await assistantClient.GetAsync($"/api/students/{student.Id}"));
            var assistantListBody = await (await assistantClient.GetAsync("/api/students")).Content.ReadAsStringAsync();

            Assert.Equal("01155501234", seenByTeacher.ParentWhatsApp);
            Assert.Equal("011****1234", seenByAssistant.ParentWhatsApp);
            Assert.DoesNotContain("01155501234", assistantListBody);
        }

        [Fact]
        public async Task An_assistant_editing_a_student_does_not_lose_the_hidden_parent_phone()
        {
            var teacher = await _api.CreateTeacherAsync();
            var teacherClient = _api.TeacherClient(teacher);
            var (_, session, _) = await _api.CreateAssistantAsync(teacher);
            var assistantClient = _api.WithToken(session.AccessToken);
            var group = await _api.CreateGroupAsync(teacherClient);
            var student = await _api.CreateStudentAsync(teacherClient, group.Id);

            // The assistant renames the student and sends no parent phone (they never saw it).
            var update = await assistantClient.PutAsJsonAsync($"/api/students/{student.Id}", new UpdateStudentRequest
            {
                FullName = "Renamed By Assistant",
                GroupId = student.GroupId,
                ParentName = student.ParentName,
                ParentWhatsApp = null,
                ParentConsent = true,
            });
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);

            var seenByTeacher = await TestApi.ReadAsync<StudentDto>(await teacherClient.GetAsync($"/api/students/{student.Id}"));
            Assert.Equal("Renamed By Assistant", seenByTeacher.FullName);
            Assert.Equal("01155501234", seenByTeacher.ParentWhatsApp);
        }

        [Fact]
        public async Task Everything_an_assistant_changes_is_recorded_and_the_teachers_own_changes_are_not()
        {
            var teacher = await _api.CreateTeacherAsync();
            var teacherClient = _api.TeacherClient(teacher);
            var (_, session, assistant) = await _api.CreateAssistantAsync(teacher);
            var assistantClient = _api.WithToken(session.AccessToken);

            // The teacher's own work leaves no log rows.
            var teacherGroup = await _api.CreateGroupAsync(teacherClient, "Made by teacher");
            Assert.Empty(await ReadLogAsync(teacherClient));

            // The assistant adds a student, renames them, and turns a group off.
            var student = await _api.CreateStudentAsync(assistantClient, teacherGroup.Id, fullName: "Added By Assistant");
            await assistantClient.PutAsJsonAsync($"/api/students/{student.Id}", new UpdateStudentRequest
            {
                FullName = "Renamed",
                GroupId = student.GroupId,
                ParentName = student.ParentName,
                ParentConsent = true,
            });
            var extraGroup = await _api.CreateGroupAsync(assistantClient, "Made by assistant");
            await assistantClient.PutAsJsonAsync($"/api/groups/{extraGroup.Id}/status", new SetActiveRequest { IsActive = false });
            await assistantClient.DeleteAsync($"/api/groups/{extraGroup.Id}");

            var log = await ReadLogAsync(teacherClient);

            // Every row names the assistant.
            Assert.NotEmpty(log);
            Assert.All(log, row => Assert.Equal(assistant.Id, row.UserId));
            Assert.All(log, row => Assert.Equal(assistant.FullName, row.UserName));

            // Adding a student creates the person and the student record.
            Assert.Contains(log, row => row.Action == "Created" && row.EntityName == "Student" && row.EntityId == student.Id.ToString());
            Assert.Contains(log, row => row.Action == "Created" && row.EntityName == "User" && row.Details!.Contains("Added By Assistant"));

            // The rename shows the old and the new value.
            var rename = Assert.Single(log, row => row.Action == "Updated" && row.EntityName == "User");
            Assert.Contains("Added By Assistant", rename.Details);
            Assert.Contains("Renamed", rename.Details);

            // Group work: created, turned off, deleted (with its lesson times).
            var groupId = extraGroup.Id.ToString();
            Assert.Contains(log, row => row.Action == "Created" && row.EntityName == "Group" && row.EntityId == groupId);
            Assert.Contains(log, row => row.Action == "Updated" && row.EntityName == "Group" && row.EntityId == groupId);
            Assert.Contains(log, row => row.Action == "Deleted" && row.EntityName == "Group" && row.EntityId == groupId);

            // Newest first.
            Assert.Equal(log.OrderByDescending(row => row.CreatedAt).ThenByDescending(row => row.Id).Select(row => row.Id), log.Select(row => row.Id));

            // The filter by assistant works, and an unknown assistant gives an empty list.
            Assert.Equal(log.Count, (await ReadLogAsync(teacherClient, $"?pageSize=100&userId={assistant.Id}")).Count);
            Assert.Empty(await ReadLogAsync(teacherClient, "?pageSize=100&userId=999999"));
        }

        [Fact]
        public async Task A_failed_change_leaves_no_log_row()
        {
            var teacher = await _api.CreateTeacherAsync();
            var teacherClient = _api.TeacherClient(teacher);
            var (_, session, _) = await _api.CreateAssistantAsync(teacher);
            var assistantClient = _api.WithToken(session.AccessToken);

            // Refused by a rule: the group does not exist.
            var refused = await TestApi.PostAsync(assistantClient, "/api/students", TestApi.NewStudentRequest(999_999));
            Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);

            Assert.Empty(await ReadLogAsync(teacherClient));
        }

        [Fact]
        public async Task Suspending_an_assistant_stops_them_at_once_and_restoring_gives_access_back()
        {
            var teacher = await _api.CreateTeacherAsync();
            var teacherClient = _api.TeacherClient(teacher);
            var (_, session, assistant) = await _api.CreateAssistantAsync(teacher);
            var assistantClient = _api.WithToken(session.AccessToken);
            Assert.Equal(HttpStatusCode.OK, (await assistantClient.GetAsync("/api/students")).StatusCode);

            var suspend = await teacherClient.PutAsJsonAsync($"/api/assistants/{assistant.Id}/status", new SetActiveRequest { IsActive = false });
            Assert.Equal(HttpStatusCode.NoContent, suspend.StatusCode);

            // The assistant's token is still valid in time, but it must not work any more.
            var whileSuspended = await assistantClient.GetAsync("/api/students");
            await TestApi.AssertProblemAsync(whileSuspended, HttpStatusCode.Forbidden, "Auth.TenantNotAvailable");

            var list = await TestApi.ReadAsync<List<AssistantDto>>(await teacherClient.GetAsync("/api/assistants"));
            Assert.False(Assert.Single(list).IsActive);

            await teacherClient.PutAsJsonAsync($"/api/assistants/{assistant.Id}/status", new SetActiveRequest { IsActive = true });
            Assert.Equal(HttpStatusCode.OK, (await assistantClient.GetAsync("/api/students")).StatusCode);
        }

        [Fact]
        public async Task The_status_endpoint_for_assistants_cannot_be_used_on_students_or_the_teacher()
        {
            var teacher = await _api.CreateTeacherAsync();
            var teacherClient = _api.TeacherClient(teacher);
            var group = await _api.CreateGroupAsync(teacherClient);
            await _api.CreateStudentAsync(teacherClient, group.Id);

            // User number 1 is the teacher, user number 2 is the student. Neither is an assistant.
            var onTeacher = await teacherClient.PutAsJsonAsync("/api/assistants/1/status", new SetActiveRequest { IsActive = false });
            var onStudent = await teacherClient.PutAsJsonAsync("/api/assistants/2/status", new SetActiveRequest { IsActive = false });

            await TestApi.AssertProblemAsync(onTeacher, HttpStatusCode.NotFound, "Assistant.NotFound");
            await TestApi.AssertProblemAsync(onStudent, HttpStatusCode.NotFound, "Assistant.NotFound");

            // The teacher still has access.
            Assert.Equal(HttpStatusCode.OK, (await teacherClient.GetAsync("/api/students")).StatusCode);
        }
    }
}
