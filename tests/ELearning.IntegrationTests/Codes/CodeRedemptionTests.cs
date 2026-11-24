using ELearning.IntegrationTests.Infrastructure;
using ELearning.Shared.DTOs;
using ELearning.Shared.DTOs.Codes;
using ELearning.Shared.DTOs.Teaching;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace ELearning.IntegrationTests.Codes
{
    // A logged-in student enters a code and gets days of access.
    [Collection(ApiCollection.Name)]
    public class CodeRedemptionTests
    {
        private static readonly TimeSpan FewSeconds = TimeSpan.FromSeconds(30);

        private readonly ApiFixture _fixture;
        private readonly TestApi _api;

        public CodeRedemptionTests(ApiFixture fixture)
        {
            _fixture = fixture;
            _api = new TestApi(fixture);
        }

        private static void AssertClose(DateTime expected, DateTime actual)
        {
            Assert.True((expected - actual).Duration() < FewSeconds, $"Expected about {expected:O} but got {actual:O}");
        }

        [Fact]
        public async Task A_free_week_code_gives_exactly_7_days_starting_now()
        {
            var teacher = await _api.CreateTeacherAsync();
            var (student, client) = await _api.NewLoggedInStudentAsync(teacher);
            Assert.Equal(SubscriptionState.NotActivated, (await TestApi.SubscriptionStatusAsync(client)).Status);

            var response = await TestApi.RedeemAsync(client, await _api.NewCodeAsync(CodeKind.FreeWeek));
            var result = await TestApi.ReadAsync<RedeemResultDto>(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(CodeKind.FreeWeek, result.Type);
            Assert.Equal(7, result.DurationDays);
            AssertClose(DateTime.UtcNow, result.StartsAt);
            Assert.Equal(TimeSpan.FromDays(7), result.EndsAt - result.StartsAt);

            var status = await TestApi.SubscriptionStatusAsync(client);
            Assert.Equal(SubscriptionState.Trial, status.Status);
            Assert.Equal(7, status.DaysLeft);
            Assert.False(status.ExpiringSoon);

            // The teacher sees the student as activated and in the free week.
            var seenByTeacher = await TestApi.ReadAsync<StudentDto>(await _api.TeacherClient(teacher).GetAsync($"/api/students/{student.Id}"));
            Assert.True(seenByTeacher.IsActivated);
            Assert.Equal(SubscriptionState.Trial, seenByTeacher.Subscription);
            Assert.NotNull(seenByTeacher.AccessEndsAt);
        }

        [Fact]
        public async Task A_30_day_code_gives_exactly_30_days()
        {
            var teacher = await _api.SharedTeacherAsync();
            var (_, client) = await _api.NewLoggedInStudentAsync(teacher);

            var result = await TestApi.ReadAsync<RedeemResultDto>(
                await TestApi.RedeemAsync(client, await _api.NewCodeAsync(CodeKind.Subscription30)));

            Assert.Equal(CodeKind.Subscription30, result.Type);
            Assert.Equal(30, result.DurationDays);
            AssertClose(DateTime.UtcNow, result.StartsAt);
            Assert.Equal(TimeSpan.FromDays(30), result.EndsAt - result.StartsAt);

            var status = await TestApi.SubscriptionStatusAsync(client);
            Assert.Equal(SubscriptionState.Active, status.Status);
            Assert.Equal(30, status.DaysLeft);
        }

        [Fact]
        public async Task Renewing_before_the_end_adds_the_new_days_after_the_current_ones()
        {
            var teacher = await _api.SharedTeacherAsync();
            var (_, client) = await _api.NewLoggedInStudentAsync(teacher);
            var freeWeek = await TestApi.ReadAsync<RedeemResultDto>(
                await TestApi.RedeemAsync(client, await _api.NewCodeAsync(CodeKind.FreeWeek)));

            var paid = await TestApi.ReadAsync<RedeemResultDto>(
                await TestApi.RedeemAsync(client, await _api.NewCodeAsync(CodeKind.Subscription30)));

            // The student loses nothing: the 30 days begin when the free week ends.
            // (Compared with a small tolerance: the database keeps times to the microsecond,
            // which is slightly less exact than the value the first answer was built from.)
            AssertClose(freeWeek.EndsAt, paid.StartsAt);
            Assert.Equal(TimeSpan.FromDays(30), paid.EndsAt - paid.StartsAt);

            var status = await TestApi.SubscriptionStatusAsync(client);
            Assert.Equal(SubscriptionState.Active, status.Status);
            Assert.Equal(37, status.DaysLeft);
        }

        [Fact]
        public async Task After_the_days_run_out_a_new_code_starts_from_the_day_it_is_entered()
        {
            var teacher = await _api.SharedTeacherAsync();
            var (_, client) = await _api.NewLoggedInStudentAsync(teacher);
            await TestApi.RedeemAsync(client, await _api.NewCodeAsync(CodeKind.FreeWeek));
            var paidCode = await _api.NewCodeAsync(CodeKind.Subscription30);

            try
            {
                // Day 10: the free week ended three days ago.
                _fixture.Clock.Offset = TimeSpan.FromDays(10);

                var expired = await TestApi.SubscriptionStatusAsync(client);
                Assert.Equal(SubscriptionState.Expired, expired.Status);
                Assert.Equal(0, expired.DaysLeft);

                var paid = await TestApi.ReadAsync<RedeemResultDto>(await TestApi.RedeemAsync(client, paidCode));

                // The gap is not charged: the 30 days start on day 10, not on day 7.
                AssertClose(_api.ApiNow(), paid.StartsAt);
                Assert.Equal(TimeSpan.FromDays(30), paid.EndsAt - paid.StartsAt);
                Assert.Equal(SubscriptionState.Active, (await TestApi.SubscriptionStatusAsync(client)).Status);
            }
            finally
            {
                _fixture.Clock.Offset = TimeSpan.Zero;
            }
        }

        [Fact]
        public async Task The_student_is_warned_in_the_last_three_days()
        {
            var teacher = await _api.SharedTeacherAsync();
            var (_, client) = await _api.NewLoggedInStudentAsync(teacher);
            await TestApi.RedeemAsync(client, await _api.NewCodeAsync(CodeKind.FreeWeek));

            try
            {
                _fixture.Clock.Offset = TimeSpan.FromDays(3);
                Assert.False((await TestApi.SubscriptionStatusAsync(client)).ExpiringSoon);

                _fixture.Clock.Offset = TimeSpan.FromDays(5);
                var nearTheEnd = await TestApi.SubscriptionStatusAsync(client);
                Assert.True(nearTheEnd.ExpiringSoon);
                Assert.Equal(2, nearTheEnd.DaysLeft);
            }
            finally
            {
                _fixture.Clock.Offset = TimeSpan.Zero;
            }
        }

        [Theory]
        [InlineData("AAAA-BBBB-CCCC")]      // right shape, does not exist
        [InlineData("SHORT")]
        [InlineData("' OR '1'='1")]
        [InlineData("AAAA-BBBB-CCCC-DDDD-EEEE")]
        public async Task A_code_that_does_not_exist_gives_no_access(string wrongCode)
        {
            var teacher = await _api.SharedTeacherAsync();
            var (_, client) = await _api.NewLoggedInStudentAsync(teacher);

            var response = await TestApi.RedeemAsync(client, wrongCode);

            await TestApi.AssertProblemAsync(response, HttpStatusCode.BadRequest, "Code.Invalid");
            Assert.Equal(SubscriptionState.NotActivated, (await TestApi.SubscriptionStatusAsync(client)).Status);
        }

        [Fact]
        public async Task A_code_typed_in_small_letters_without_dashes_still_works()
        {
            var teacher = await _api.SharedTeacherAsync();
            var (_, client) = await _api.NewLoggedInStudentAsync(teacher);
            var code = await _api.NewCodeAsync(CodeKind.FreeWeek);

            var response = await TestApi.RedeemAsync(client, " " + code.Replace("-", "").ToLowerInvariant() + " ");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task A_code_works_only_once()
        {
            var teacher = await _api.SharedTeacherAsync();
            var (_, firstStudent) = await _api.NewLoggedInStudentAsync(teacher);
            var (_, secondStudent) = await _api.NewLoggedInStudentAsync(teacher);
            var code = await _api.NewCodeAsync(CodeKind.Subscription30);

            var first = await TestApi.RedeemAsync(firstStudent, code);
            var sameStudentAgain = await TestApi.RedeemAsync(firstStudent, code);
            var otherStudent = await TestApi.RedeemAsync(secondStudent, code);

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            await TestApi.AssertProblemAsync(sameStudentAgain, HttpStatusCode.Conflict, "Code.AlreadyUsed");
            await TestApi.AssertProblemAsync(otherStudent, HttpStatusCode.Conflict, "Code.AlreadyUsed");

            // The first student got 30 days once (not 60), and the second student got nothing.
            Assert.Equal(30, (await TestApi.SubscriptionStatusAsync(firstStudent)).DaysLeft);
            Assert.Equal(SubscriptionState.NotActivated, (await TestApi.SubscriptionStatusAsync(secondStudent)).Status);
        }

        [Fact]
        public async Task When_two_students_send_the_same_code_at_the_same_moment_only_one_wins()
        {
            var teacher = await _api.SharedTeacherAsync();
            var (_, firstStudent) = await _api.NewLoggedInStudentAsync(teacher);
            var (_, secondStudent) = await _api.NewLoggedInStudentAsync(teacher);
            var batch = await _api.GenerateCodesAsync(CodeKind.Subscription30);
            var code = batch.Codes.Single();

            // Both requests are started before either one finishes.
            var responses = await Task.WhenAll(
                TestApi.RedeemAsync(firstStudent, code),
                TestApi.RedeemAsync(secondStudent, code));

            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);

            var statuses = new[]
            {
                await TestApi.SubscriptionStatusAsync(firstStudent),
                await TestApi.SubscriptionStatusAsync(secondStudent),
            };
            Assert.Single(statuses, s => s.Status == SubscriptionState.Active);
            Assert.Single(statuses, s => s.Status == SubscriptionState.NotActivated);
        }

        [Fact]
        public async Task The_same_student_sending_one_code_many_times_at_once_gets_the_days_only_once()
        {
            var teacher = await _api.SharedTeacherAsync();
            var (_, client) = await _api.NewLoggedInStudentAsync(teacher);
            var code = await _api.NewCodeAsync(CodeKind.Subscription30);

            // For example the student taps the button five times quickly.
            var attempts = Enumerable.Range(0, 5).Select(_ => TestApi.RedeemAsync(client, code)).ToArray();
            var responses = await Task.WhenAll(attempts);

            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Equal(30, (await TestApi.SubscriptionStatusAsync(client)).DaysLeft);
        }

        [Fact]
        public async Task A_code_cannot_be_redeemed_after_its_last_redeem_date()
        {
            var teacher = await _api.SharedTeacherAsync();
            var (_, client) = await _api.NewLoggedInStudentAsync(teacher);
            var batch = await _api.GenerateCodesAsync(CodeKind.Subscription30, redeemBefore: DateTime.UtcNow.AddDays(1));

            try
            {
                _fixture.Clock.Offset = TimeSpan.FromDays(2);
                var response = await TestApi.RedeemAsync(client, batch.Codes.Single());

                await TestApi.AssertProblemAsync(response, HttpStatusCode.Conflict, "Code.Expired");
                Assert.Equal(SubscriptionState.NotActivated, (await TestApi.SubscriptionStatusAsync(client)).Status);
            }
            finally
            {
                _fixture.Clock.Offset = TimeSpan.Zero;
            }
        }

        [Fact]
        public async Task A_code_redeemed_one_day_before_its_last_redeem_date_still_gives_all_its_days()
        {
            var teacher = await _api.SharedTeacherAsync();
            var (_, client) = await _api.NewLoggedInStudentAsync(teacher);
            var batch = await _api.GenerateCodesAsync(CodeKind.Subscription30, redeemBefore: DateTime.UtcNow.AddDays(1));

            var result = await TestApi.ReadAsync<RedeemResultDto>(await TestApi.RedeemAsync(client, batch.Codes.Single()));

            // The redeem date only limits WHEN the code can be entered, not how long the access lasts.
            Assert.Equal(TimeSpan.FromDays(30), result.EndsAt - result.StartsAt);
        }

        [Fact]
        public async Task The_free_week_is_given_once_per_teacher_and_a_refused_code_is_not_used_up()
        {
            var ahmed = await _api.CreateTeacherAsync();
            var mohamed = await _api.CreateTeacherAsync();
            var (student, atAhmed) = await _api.NewLoggedInStudentAsync(ahmed);
            await TestApi.RedeemAsync(atAhmed, await _api.NewCodeAsync(CodeKind.FreeWeek));

            var secondFreeWeek = await _api.NewCodeAsync(CodeKind.FreeWeek);
            var refused = await TestApi.RedeemAsync(atAhmed, secondFreeWeek);
            await TestApi.AssertProblemAsync(refused, HttpStatusCode.Conflict, "Code.FreeWeekAlreadyUsed");
            Assert.Equal(7, (await TestApi.SubscriptionStatusAsync(atAhmed)).DaysLeft);

            // The same person at another teacher has not had a free week there yet,
            // and the code that was refused above is still usable.
            var (_, atMohamed) = await _api.NewLoggedInStudentAsync(mohamed, student.PhoneNumber);
            Assert.Equal(SubscriptionState.NotActivated, (await TestApi.SubscriptionStatusAsync(atMohamed)).Status);

            var accepted = await TestApi.RedeemAsync(atMohamed, secondFreeWeek);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            Assert.Equal(SubscriptionState.Trial, (await TestApi.SubscriptionStatusAsync(atMohamed)).Status);
        }

        [Fact]
        public async Task Days_bought_at_one_teacher_do_not_open_another_teachers_platform()
        {
            var ahmed = await _api.CreateTeacherAsync();
            var mohamed = await _api.CreateTeacherAsync();
            var (student, atAhmed) = await _api.NewLoggedInStudentAsync(ahmed);
            var (_, atMohamed) = await _api.NewLoggedInStudentAsync(mohamed, student.PhoneNumber);

            await TestApi.RedeemAsync(atAhmed, await _api.NewCodeAsync(CodeKind.Subscription30));

            Assert.Equal(SubscriptionState.Active, (await TestApi.SubscriptionStatusAsync(atAhmed)).Status);
            Assert.Equal(SubscriptionState.NotActivated, (await TestApi.SubscriptionStatusAsync(atMohamed)).Status);
        }

        [Fact]
        public async Task Five_wrong_codes_block_code_entry_for_a_while_even_for_a_correct_code()
        {
            var teacher = await _api.SharedTeacherAsync();
            var (_, client) = await _api.NewLoggedInStudentAsync(teacher);
            var goodCode = await _api.NewCodeAsync(CodeKind.FreeWeek);

            for (var attempt = 1; attempt <= 5; attempt++)
            {
                var wrong = await TestApi.RedeemAsync(client, "AAAA-BBBB-CCC" + attempt);
                Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
            }

            var whileBlocked = await TestApi.RedeemAsync(client, goodCode);
            await TestApi.AssertProblemAsync(whileBlocked, HttpStatusCode.Forbidden, "Code.TooManyAttempts");

            try
            {
                _fixture.Clock.Offset = TimeSpan.FromMinutes(16);
                var afterWaiting = await TestApi.RedeemAsync(client, goodCode);
                Assert.Equal(HttpStatusCode.OK, afterWaiting.StatusCode);
            }
            finally
            {
                _fixture.Clock.Offset = TimeSpan.Zero;
            }
        }

        [Fact]
        public async Task Only_students_have_a_subscription()
        {
            var teacher = await _api.CreateTeacherAsync();
            var (_, assistantSession, _) = await _api.CreateAssistantAsync(teacher);
            var code = await _api.NewCodeAsync(CodeKind.Subscription30);

            foreach (var staffClient in new[] { _api.TeacherClient(teacher), _api.WithToken(assistantSession.AccessToken) })
            {
                Assert.Equal(HttpStatusCode.Forbidden, (await TestApi.RedeemAsync(staffClient, code)).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await staffClient.GetAsync("/api/subscription")).StatusCode);
            }

            Assert.Equal(HttpStatusCode.Unauthorized, (await TestApi.RedeemAsync(_api.Anonymous(), code)).StatusCode);

            // The admin is logged in but is not a student at any teacher.
            var admin = await _api.AdminClientAsync();
            Assert.Equal(HttpStatusCode.Forbidden, (await TestApi.RedeemAsync(admin, code)).StatusCode);
        }

        [Fact]
        public async Task The_teacher_can_list_students_whose_days_are_over_or_almost_over()
        {
            var teacher = await _api.CreateTeacherAsync();
            var teacherClient = _api.TeacherClient(teacher);
            var (weekStudent, weekClient) = await _api.NewLoggedInStudentAsync(teacher);
            var (monthStudent, monthClient) = await _api.NewLoggedInStudentAsync(teacher);
            await _api.NewLoggedInStudentAsync(teacher);   // a third student who never activates
            await TestApi.RedeemAsync(weekClient, await _api.NewCodeAsync(CodeKind.FreeWeek));
            await TestApi.RedeemAsync(monthClient, await _api.NewCodeAsync(CodeKind.Subscription30));

            async Task<List<StudentDto>> FilterAsync(string filter)
            {
                var response = await teacherClient.GetAsync("/api/students?subscription=" + filter);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                return (await TestApi.ReadAsync<PaginatedResult<StudentDto>>(response)).Data.ToList();
            }

            try
            {
                // Day 5: the free week has 2 days left, the month has 25.
                _fixture.Clock.Offset = TimeSpan.FromDays(5);
                Assert.Equal(weekStudent.Id, Assert.Single(await FilterAsync("ExpiringSoon")).Id);
                Assert.Empty(await FilterAsync("Expired"));

                // Day 8: the free week is over.
                _fixture.Clock.Offset = TimeSpan.FromDays(8);
                var expired = Assert.Single(await FilterAsync("Expired"));
                Assert.Equal(weekStudent.Id, expired.Id);
                Assert.Equal(SubscriptionState.Expired, expired.Subscription);
                Assert.Empty(await FilterAsync("ExpiringSoon"));

                var monthNow = await TestApi.ReadAsync<StudentDto>(await teacherClient.GetAsync($"/api/students/{monthStudent.Id}"));
                Assert.Equal(SubscriptionState.Active, monthNow.Subscription);
            }
            finally
            {
                _fixture.Clock.Offset = TimeSpan.Zero;
            }
        }

        [Fact]
        public async Task Each_redemption_is_stored_as_one_period_linked_to_its_code()
        {
            var teacher = await _api.CreateTeacherAsync();
            var (_, client) = await _api.NewLoggedInStudentAsync(teacher);
            await TestApi.RedeemAsync(client, await _api.NewCodeAsync(CodeKind.FreeWeek));
            await TestApi.RedeemAsync(client, await _api.NewCodeAsync(CodeKind.Subscription30));

            var usedCodes = await _api.ReadHubAsync(hub => hub.Codes
                .Where(c => c.UsedInTenantId == teacher.Tenant.Id)
                .CountAsync());

            // Two codes were used at this teacher, each exactly once.
            Assert.Equal(2, usedCodes);
        }
    }
}
