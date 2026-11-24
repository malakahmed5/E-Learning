using ELearning.IntegrationTests.Infrastructure;
using ELearning.Shared.DTOs;
using ELearning.Shared.DTOs.Codes;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.RegularExpressions;

namespace ELearning.IntegrationTests.Codes
{
    // The platform owner generates, lists, counts, exports and revokes codes.
    [Collection(ApiCollection.Name)]
    public class CodeAdminTests
    {
        private readonly ApiFixture _fixture;
        private readonly TestApi _api;

        public CodeAdminTests(ApiFixture fixture)
        {
            _fixture = fixture;
            _api = new TestApi(fixture);
        }

        private static async Task<PaginatedResult<CodeDto>> ListAsync(HttpClient admin, string query)
        {
            var response = await admin.GetAsync("/api/admin/codes" + query);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await TestApi.ReadAsync<PaginatedResult<CodeDto>>(response);
        }

        private static async Task<CodeStatsDto> StatsAsync(HttpClient admin, CodeKind type)
        {
            var stats = await TestApi.ReadAsync<List<CodeStatsDto>>(await admin.GetAsync("/api/admin/codes/stats"));
            return stats.Single(s => s.Type == type);
        }

        [Fact]
        public async Task A_batch_of_free_week_codes_is_generated_with_unique_readable_codes()
        {
            var batch = await _api.GenerateCodesAsync(CodeKind.FreeWeek, count: 25, note: "September campaign");

            Assert.Equal(CodeKind.FreeWeek, batch.Type);
            Assert.Equal(7, batch.DurationDays);
            Assert.Null(batch.RedeemBefore);
            Assert.Equal(25, batch.Codes.Count);
            Assert.Equal(25, batch.Codes.Distinct().Count());
            Assert.All(batch.Codes, code => Assert.Matches(new Regex("^[A-HJ-KM-NP-Z2-9]{4}-[A-HJ-KM-NP-Z2-9]{4}-[A-HJ-KM-NP-Z2-9]{4}$"), code));

            var admin = await _api.AdminClientAsync();
            var listed = await ListAsync(admin, $"?batchId={batch.BatchId}&pageSize=100");
            Assert.Equal(25, listed.Count);
            Assert.All(listed.Data, code => Assert.Equal(CodeState.Available, code.Status));
            Assert.All(listed.Data, code => Assert.Equal("September campaign", code.BatchNote));
        }

        [Fact]
        public async Task A_single_30_day_code_can_be_generated_with_a_last_redeem_date()
        {
            var redeemBefore = DateTime.UtcNow.AddDays(10);

            var batch = await _api.GenerateCodesAsync(CodeKind.Subscription30, count: 1, redeemBefore: redeemBefore);

            Assert.Equal(30, batch.DurationDays);
            Assert.Single(batch.Codes);
            Assert.NotNull(batch.RedeemBefore);
            // The redeem date and the 30 days are two different things.
            Assert.True(Math.Abs((batch.RedeemBefore!.Value - redeemBefore).TotalSeconds) < 2);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        [InlineData(2001)]
        public async Task An_unreasonable_number_of_codes_is_refused(int count)
        {
            var admin = await _api.AdminClientAsync();

            var response = await TestApi.PostAsync(admin, "/api/admin/codes/batches", new { type = "FreeWeek", count });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task An_unknown_code_type_or_a_redeem_date_in_the_past_is_refused()
        {
            var admin = await _api.AdminClientAsync();

            var unknownType = await TestApi.PostAsync(admin, "/api/admin/codes/batches", new { type = "Lifetime", count = 1 });
            var pastDate = await TestApi.PostAsync(admin, "/api/admin/codes/batches",
                new GenerateCodesRequest { Type = CodeKind.FreeWeek, Count = 1, RedeemBefore = DateTime.UtcNow.AddDays(-1) });

            Assert.Equal(HttpStatusCode.BadRequest, unknownType.StatusCode);
            await TestApi.AssertProblemAsync(pastDate, HttpStatusCode.BadRequest, "Code.RedeemBeforeInPast");
        }

        [Fact]
        public async Task Only_the_platform_owner_can_use_the_code_management_endpoints()
        {
            var teacher = await _api.SharedTeacherAsync();
            var (_, studentClient) = await _api.NewLoggedInStudentAsync(teacher);
            var batch = await _api.GenerateCodesAsync(CodeKind.FreeWeek);
            var codeId = (await ListAsync(await _api.AdminClientAsync(), $"?batchId={batch.BatchId}")).Data.Single().Id;

            foreach (var client in new[] { _api.TeacherClient(teacher), studentClient })
            {
                var responses = new[]
                {
                    await TestApi.PostAsync(client, "/api/admin/codes/batches", new GenerateCodesRequest { Type = CodeKind.Subscription30, Count = 1 }),
                    await client.GetAsync("/api/admin/codes"),
                    await client.GetAsync("/api/admin/codes/stats"),
                    await client.PostAsync($"/api/admin/codes/{codeId}/revoke", null),
                    await client.GetAsync($"/api/admin/codes/batches/{batch.BatchId}/export"),
                    await client.GetAsync("/api/admin/audit-logs"),
                };
                Assert.All(responses, response => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode));
            }

            var anonymous = await _api.Anonymous().GetAsync("/api/admin/codes");
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

            // Nothing happened to the code.
            var stillAvailable = (await ListAsync(await _api.AdminClientAsync(), $"?batchId={batch.BatchId}")).Data.Single();
            Assert.Equal(CodeState.Available, stillAvailable.Status);
        }

        [Fact]
        public async Task An_unused_code_can_be_revoked_and_then_cannot_be_redeemed()
        {
            var admin = await _api.AdminClientAsync();
            var teacher = await _api.SharedTeacherAsync();
            var (_, studentClient) = await _api.NewLoggedInStudentAsync(teacher);
            var batch = await _api.GenerateCodesAsync(CodeKind.Subscription30);
            var code = (await ListAsync(admin, $"?batchId={batch.BatchId}")).Data.Single();

            var revoke = await admin.PostAsync($"/api/admin/codes/{code.Id}/revoke", null);
            Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
            Assert.Equal(CodeState.Revoked, (await ListAsync(admin, $"?batchId={batch.BatchId}")).Data.Single().Status);

            var redeem = await TestApi.RedeemAsync(studentClient, code.Code);
            await TestApi.AssertProblemAsync(redeem, HttpStatusCode.Conflict, "Code.Revoked");
            Assert.Equal(SubscriptionState.NotActivated, (await TestApi.SubscriptionStatusAsync(studentClient)).Status);

            var revokeAgain = await admin.PostAsync($"/api/admin/codes/{code.Id}/revoke", null);
            await TestApi.AssertProblemAsync(revokeAgain, HttpStatusCode.Conflict, "Code.Revoked");
        }

        [Fact]
        public async Task A_redeemed_code_cannot_be_revoked_and_shows_who_redeemed_it()
        {
            var admin = await _api.AdminClientAsync();
            var teacher = await _api.CreateTeacherAsync("Mr Redeem Owner");
            var (student, studentClient) = await _api.NewLoggedInStudentAsync(teacher);
            var batch = await _api.GenerateCodesAsync(CodeKind.Subscription30);
            Assert.Equal(HttpStatusCode.OK, (await TestApi.RedeemAsync(studentClient, batch.Codes.Single())).StatusCode);

            var code = (await ListAsync(admin, $"?batchId={batch.BatchId}")).Data.Single();
            Assert.Equal(CodeState.Redeemed, code.Status);
            Assert.Equal(student.FullName, code.RedeemedByName);
            Assert.Equal(student.PhoneNumber, code.RedeemedByPhone);
            Assert.Equal("Mr Redeem Owner", code.RedeemedAtTeacher);
            Assert.NotNull(code.RedeemedAt);

            var revoke = await admin.PostAsync($"/api/admin/codes/{code.Id}/revoke", null);
            await TestApi.AssertProblemAsync(revoke, HttpStatusCode.Conflict, "Code.AlreadyUsed");

            var unknown = await admin.PostAsync("/api/admin/codes/999999999/revoke", null);
            await TestApi.AssertProblemAsync(unknown, HttpStatusCode.NotFound, "Code.NotFound");
        }

        [Fact]
        public async Task The_table_can_be_filtered_and_searched()
        {
            var admin = await _api.AdminClientAsync();
            var teacher = await _api.SharedTeacherAsync();
            var (student, studentClient) = await _api.NewLoggedInStudentAsync(teacher);
            var batch = await _api.GenerateCodesAsync(CodeKind.Subscription30, count: 4);
            var all = (await ListAsync(admin, $"?batchId={batch.BatchId}")).Data.ToList();

            await TestApi.RedeemAsync(studentClient, all[0].Code);
            await admin.PostAsync($"/api/admin/codes/{all[1].Id}/revoke", null);

            Assert.Equal(1, (await ListAsync(admin, $"?batchId={batch.BatchId}&status=Redeemed")).Count);
            Assert.Equal(1, (await ListAsync(admin, $"?batchId={batch.BatchId}&status=Revoked")).Count);
            Assert.Equal(2, (await ListAsync(admin, $"?batchId={batch.BatchId}&status=Available")).Count);
            Assert.Equal(0, (await ListAsync(admin, $"?batchId={batch.BatchId}&status=Expired")).Count);
            Assert.Equal(4, (await ListAsync(admin, $"?batchId={batch.BatchId}&type=Subscription30")).Count);
            Assert.Equal(0, (await ListAsync(admin, $"?batchId={batch.BatchId}&type=FreeWeek")).Count);

            // Search by the code itself (typed with dashes) and by the redeemer's phone number.
            var byCode = await ListAsync(admin, "?search=" + Uri.EscapeDataString(all[2].Code));
            Assert.Equal(all[2].Id, Assert.Single(byCode.Data).Id);
            var byPhone = await ListAsync(admin, "?search=" + student.PhoneNumber);
            Assert.Equal(all[0].Id, Assert.Single(byPhone.Data).Id);

            // Pages.
            var firstPage = await ListAsync(admin, $"?batchId={batch.BatchId}&pageSize=3");
            Assert.Equal(4, firstPage.Count);
            Assert.Equal(3, firstPage.Data.Count());
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/admin/codes?pageSize=1000")).StatusCode);
        }

        [Fact]
        public async Task Codes_whose_redeem_date_has_passed_are_shown_and_counted_as_expired()
        {
            var admin = await _api.AdminClientAsync();
            var before = await StatsAsync(admin, CodeKind.FreeWeek);
            var batch = await _api.GenerateCodesAsync(CodeKind.FreeWeek, count: 3, redeemBefore: DateTime.UtcNow.AddDays(1));

            var afterGenerating = await StatsAsync(admin, CodeKind.FreeWeek);
            Assert.Equal(before.Total + 3, afterGenerating.Total);
            Assert.Equal(before.Available + 3, afterGenerating.Available);

            try
            {
                // Two days later the last redeem date is over.
                _fixture.Clock.Offset = TimeSpan.FromDays(2);

                var listed = await ListAsync(admin, $"?batchId={batch.BatchId}");
                Assert.All(listed.Data, code => Assert.Equal(CodeState.Expired, code.Status));
                Assert.Equal(3, (await ListAsync(admin, $"?batchId={batch.BatchId}&status=Expired")).Count);

                var afterExpiry = await StatsAsync(admin, CodeKind.FreeWeek);
                Assert.True(afterExpiry.Expired >= 3);
                Assert.Equal(afterExpiry.Total, afterExpiry.Available + afterExpiry.Redeemed + afterExpiry.Expired + afterExpiry.Revoked);
            }
            finally
            {
                _fixture.Clock.Offset = TimeSpan.Zero;
            }
        }

        [Fact]
        public async Task A_batch_can_be_exported()
        {
            var admin = await _api.AdminClientAsync();
            var batch = await _api.GenerateCodesAsync(CodeKind.Subscription30, count: 6);

            var exported = await TestApi.ReadAsync<List<string>>(await admin.GetAsync($"/api/admin/codes/batches/{batch.BatchId}/export"));
            var unknown = await admin.GetAsync("/api/admin/codes/batches/999999999/export");

            Assert.Equal(batch.Codes.OrderBy(c => c), exported.OrderBy(c => c));
            await TestApi.AssertProblemAsync(unknown, HttpStatusCode.NotFound, "Code.BatchNotFound");
        }

        [Fact]
        public async Task Generating_redeeming_revoking_and_exporting_are_all_written_to_the_audit_log_without_the_codes()
        {
            var admin = await _api.AdminClientAsync();
            var teacher = await _api.SharedTeacherAsync();
            var (_, studentClient) = await _api.NewLoggedInStudentAsync(teacher);
            var batch = await _api.GenerateCodesAsync(CodeKind.Subscription30, count: 2);
            var codes = (await ListAsync(admin, $"?batchId={batch.BatchId}")).Data.ToList();

            await TestApi.RedeemAsync(studentClient, codes[0].Code);
            await admin.PostAsync($"/api/admin/codes/{codes[1].Id}/revoke", null);
            await admin.GetAsync($"/api/admin/codes/batches/{batch.BatchId}/export");

            var rows = await _api.ReadHubAsync(hub => hub.AuditLogs.AsNoTracking().OrderByDescending(l => l.Id).Take(40).ToListAsync());
            var batchId = batch.BatchId.ToString();

            Assert.Contains(rows, r => r.Action == "CodesGenerated" && r.EntityName == "CodeBatch" && r.EntityId == batchId && r.AccountId != null);
            Assert.Contains(rows, r => r.Action == "CodeRedeemed" && r.EntityName == "Code" && r.EntityId == codes[0].Id.ToString());
            Assert.Contains(rows, r => r.Action == "CodeRevoked" && r.EntityName == "Code" && r.EntityId == codes[1].Id.ToString());
            Assert.Contains(rows, r => r.Action == "CodesExported" && r.EntityId == batchId);

            // The log must never hold the code text: someone who can read the log must not be able to use a code.
            foreach (var code in codes)
            {
                var withoutDashes = code.Code.Replace("-", "");
                Assert.DoesNotContain(rows, r => (r.Details ?? "").Contains(withoutDashes) || (r.Details ?? "").Contains(code.Code));
            }

            // The owner can read the log through the API.
            var apiLog = await TestApi.ReadAsync<PaginatedResult<PlatformAuditLogDto>>(await admin.GetAsync("/api/admin/audit-logs?pageSize=50"));
            Assert.Contains(apiLog.Data, r => r.Action == "CodeRevoked" && r.AccountName == "Test Admin");
        }
    }
}
