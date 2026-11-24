using ELearning.IntegrationTests.Infrastructure;
using ELearning.Shared.DTOs.Teaching;
using System.Net;
using System.Net.Http.Json;

namespace ELearning.IntegrationTests.Teaching
{
    // Centers, groups and weekly lesson times.
    [Collection(ApiCollection.Name)]
    public class CentersAndGroupsTests
    {
        private readonly TestApi _api;

        public CentersAndGroupsTests(ApiFixture fixture)
        {
            _api = new TestApi(fixture);
        }

        private static SaveGroupRequest GroupRequest(string name, int gradeLevel = 1, int? centerId = null, params ScheduleDto[] schedules)
        {
            if (schedules.Length == 0)
                schedules = [Lesson(DayOfWeek.Saturday, 16, 18)];

            return new SaveGroupRequest { Name = name, GradeLevel = gradeLevel, CenterId = centerId, Schedules = schedules.ToList() };
        }

        private static ScheduleDto Lesson(DayOfWeek day, int startHour, int endHour)
        {
            return new ScheduleDto { DayOfWeek = day, StartTime = new TimeOnly(startHour, 0), EndTime = new TimeOnly(endHour, 0) };
        }

        [Fact]
        public async Task Teacher_creates_a_center_and_a_group_with_weekly_lessons()
        {
            var teacher = await _api.CreateTeacherAsync();
            var client = _api.TeacherClient(teacher);

            var center = await TestApi.ReadAsync<CenterDto>(await TestApi.PostAsync(client, "/api/centers",
                new SaveCenterRequest { Name = "Al Nour Center", Address = "12 Tahrir St" }));

            // Lessons are sent out of order on purpose: the API returns them sorted by day.
            var createResponse = await TestApi.PostAsync(client, "/api/groups", GroupRequest("Sat and Tue", 3, center.Id,
                Lesson(DayOfWeek.Tuesday, 16, 18), Lesson(DayOfWeek.Saturday, 16, 18)));
            Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

            var groups = await TestApi.ReadAsync<List<GroupDto>>(await client.GetAsync("/api/groups"));
            var group = Assert.Single(groups);
            Assert.Equal("Sat and Tue", group.Name);
            Assert.Equal(3, group.GradeLevel);
            Assert.Equal("Al Nour Center", group.CenterName);
            Assert.True(group.IsActive);
            Assert.Equal(0, group.StudentCount);
            Assert.Equal(new[] { DayOfWeek.Tuesday, DayOfWeek.Saturday }, group.Schedules.Select(s => s.DayOfWeek));

            var centers = await TestApi.ReadAsync<List<CenterDto>>(await client.GetAsync("/api/centers"));
            Assert.Equal(1, Assert.Single(centers).GroupCount);
        }

        [Fact]
        public async Task A_group_without_a_center_is_an_online_group()
        {
            var teacher = await _api.SharedTeacherAsync();
            var client = _api.TeacherClient(teacher);

            var group = await _api.CreateGroupAsync(client, centerId: null);

            Assert.Null(group.CenterId);
            Assert.Null(group.CenterName);
        }

        [Fact]
        public async Task Invalid_groups_are_refused()
        {
            var teacher = await _api.SharedTeacherAsync();
            var client = _api.TeacherClient(teacher);

            var endsBeforeStart = await TestApi.PostAsync(client, "/api/groups",
                GroupRequest("Bad times", 1, null, Lesson(DayOfWeek.Monday, 18, 16)));
            await TestApi.AssertProblemAsync(endsBeforeStart, HttpStatusCode.BadRequest, "Group.ScheduleInvalidTimes");

            var sameLessonTwice = await TestApi.PostAsync(client, "/api/groups",
                GroupRequest("Twice", 1, null, Lesson(DayOfWeek.Monday, 16, 18), Lesson(DayOfWeek.Monday, 16, 19)));
            await TestApi.AssertProblemAsync(sameLessonTwice, HttpStatusCode.BadRequest, "Group.ScheduleDuplicate");

            var unknownCenter = await TestApi.PostAsync(client, "/api/groups", GroupRequest("No center", 1, centerId: 999_999));
            await TestApi.AssertProblemAsync(unknownCenter, HttpStatusCode.NotFound, "Center.NotFound");

            var gradeSeven = await TestApi.PostAsync(client, "/api/groups", GroupRequest("Grade 7", gradeLevel: 7));
            Assert.Equal(HttpStatusCode.BadRequest, gradeSeven.StatusCode);

            var noLessons = await TestApi.PostAsync(client, "/api/groups",
                new SaveGroupRequest { Name = "No lessons", GradeLevel = 1, Schedules = [] });
            Assert.Equal(HttpStatusCode.BadRequest, noLessons.StatusCode);
        }

        [Fact]
        public async Task Group_names_are_unique_inside_one_grade_only()
        {
            var teacher = await _api.CreateTeacherAsync();
            var client = _api.TeacherClient(teacher);
            await _api.CreateGroupAsync(client, "Group A", gradeLevel: 1);

            var sameGrade = await TestApi.PostAsync(client, "/api/groups", GroupRequest("Group A", gradeLevel: 1));
            var otherGrade = await TestApi.PostAsync(client, "/api/groups", GroupRequest("Group A", gradeLevel: 2));

            await TestApi.AssertProblemAsync(sameGrade, HttpStatusCode.Conflict, "Group.NameTaken");
            Assert.Equal(HttpStatusCode.OK, otherGrade.StatusCode);
        }

        [Fact]
        public async Task Updating_a_group_replaces_its_lessons()
        {
            var teacher = await _api.SharedTeacherAsync();
            var client = _api.TeacherClient(teacher);
            var group = await _api.CreateGroupAsync(client);

            var update = await client.PutAsJsonAsync($"/api/groups/{group.Id}",
                GroupRequest(group.Name + " renamed", group.GradeLevel, null, Lesson(DayOfWeek.Friday, 10, 12)));
            var updated = await TestApi.ReadAsync<GroupDto>(update);

            Assert.EndsWith("renamed", updated.Name);
            var onlyLesson = Assert.Single(updated.Schedules);
            Assert.Equal(DayOfWeek.Friday, onlyLesson.DayOfWeek);
        }

        [Fact]
        public async Task A_turned_off_group_is_hidden_unless_asked_for()
        {
            var teacher = await _api.CreateTeacherAsync();
            var client = _api.TeacherClient(teacher);
            var group = await _api.CreateGroupAsync(client);

            var turnOff = await client.PutAsJsonAsync($"/api/groups/{group.Id}/status", new SetActiveRequest { IsActive = false });
            Assert.Equal(HttpStatusCode.NoContent, turnOff.StatusCode);

            var activeOnly = await TestApi.ReadAsync<List<GroupDto>>(await client.GetAsync("/api/groups"));
            var all = await TestApi.ReadAsync<List<GroupDto>>(await client.GetAsync("/api/groups?includeInactive=true"));

            Assert.Empty(activeOnly);
            Assert.False(Assert.Single(all).IsActive);
        }

        [Fact]
        public async Task Things_that_are_still_in_use_cannot_be_deleted()
        {
            var teacher = await _api.CreateTeacherAsync();
            var client = _api.TeacherClient(teacher);
            var center = await TestApi.ReadAsync<CenterDto>(await TestApi.PostAsync(client, "/api/centers",
                new SaveCenterRequest { Name = "Busy Center" }));
            var group = await _api.CreateGroupAsync(client, centerId: center.Id);
            await _api.CreateStudentAsync(client, group.Id);

            var deleteCenter = await client.DeleteAsync($"/api/centers/{center.Id}");
            var deleteGroup = await client.DeleteAsync($"/api/groups/{group.Id}");

            await TestApi.AssertProblemAsync(deleteCenter, HttpStatusCode.Conflict, "Center.HasGroups");
            await TestApi.AssertProblemAsync(deleteGroup, HttpStatusCode.Conflict, "Group.HasStudents");
        }

        [Fact]
        public async Task An_empty_group_and_an_empty_center_can_be_deleted()
        {
            var teacher = await _api.SharedTeacherAsync();
            var client = _api.TeacherClient(teacher);
            var center = await TestApi.ReadAsync<CenterDto>(await TestApi.PostAsync(client, "/api/centers",
                new SaveCenterRequest { Name = "Temporary " + TestApi.NextSlug() }));
            var group = await _api.CreateGroupAsync(client, centerId: center.Id);

            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/groups/{group.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/centers/{center.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/groups/{group.Id}")).StatusCode);
        }

        [Fact]
        public async Task Two_centers_cannot_share_a_name()
        {
            var teacher = await _api.SharedTeacherAsync();
            var client = _api.TeacherClient(teacher);
            var name = "Center " + TestApi.NextSlug();
            await TestApi.PostAsync(client, "/api/centers", new SaveCenterRequest { Name = name });

            var duplicate = await TestApi.PostAsync(client, "/api/centers", new SaveCenterRequest { Name = name });

            await TestApi.AssertProblemAsync(duplicate, HttpStatusCode.Conflict, "Center.NameTaken");
        }

        [Fact]
        public async Task One_teacher_never_sees_another_teachers_groups()
        {
            var ahmed = await _api.CreateTeacherAsync();
            var mohamed = await _api.CreateTeacherAsync();
            var ahmedGroup = await _api.CreateGroupAsync(_api.TeacherClient(ahmed), "Ahmed's group");

            var mohamedClient = _api.TeacherClient(mohamed);
            var mohamedGroups = await TestApi.ReadAsync<List<GroupDto>>(await mohamedClient.GetAsync("/api/groups?includeInactive=true"));
            var directAccess = await mohamedClient.GetAsync($"/api/groups/{ahmedGroup.Id}");
            var deleteAttempt = await mohamedClient.DeleteAsync($"/api/groups/{ahmedGroup.Id}");

            // Mohamed's database is empty: the id of Ahmed's group means nothing there.
            Assert.Empty(mohamedGroups);
            Assert.Equal(HttpStatusCode.NotFound, directAccess.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, deleteAttempt.StatusCode);

            // Ahmed's group is untouched.
            Assert.Equal(HttpStatusCode.OK, (await _api.TeacherClient(ahmed).GetAsync($"/api/groups/{ahmedGroup.Id}")).StatusCode);
        }
    }
}
