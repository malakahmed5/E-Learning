using ELearning.IntegrationTests.Infrastructure;
using ELearning.Shared.DTOs.Admin;
using System.Net;

namespace ELearning.IntegrationTests.Admin
{
    [Collection(ApiCollection.Name)]
    public class AcademicTermTests
    {
        private readonly TestApi _api;

        public AcademicTermTests(ApiFixture fixture)
        {
            _api = new TestApi(fixture);
        }

        [Fact]
        public async Task Admin_creates_terms_and_overlapping_dates_are_refused()
        {
            var admin = await _api.AdminClientAsync();

            var firstTerm = await TestApi.PostAsync(admin, "/api/admin/academic-terms", new CreateAcademicTermRequest
            {
                Name = "First term 2027/2028",
                StartDate = new DateOnly(2027, 9, 18),
                EndDate = new DateOnly(2028, 1, 20),
            });
            Assert.Equal(HttpStatusCode.OK, firstTerm.StatusCode);

            // Starts before the first term ends.
            var overlapping = await TestApi.PostAsync(admin, "/api/admin/academic-terms", new CreateAcademicTermRequest
            {
                Name = "Overlapping term",
                StartDate = new DateOnly(2028, 1, 20),
                EndDate = new DateOnly(2028, 5, 30),
            });
            await TestApi.AssertProblemAsync(overlapping, HttpStatusCode.Conflict, "AcademicTerm.Overlap");

            // Starts the day after: allowed.
            var secondTerm = await TestApi.PostAsync(admin, "/api/admin/academic-terms", new CreateAcademicTermRequest
            {
                Name = "Second term 2027/2028",
                StartDate = new DateOnly(2028, 1, 21),
                EndDate = new DateOnly(2028, 5, 30),
            });
            Assert.Equal(HttpStatusCode.OK, secondTerm.StatusCode);

            var list = await TestApi.ReadAsync<List<AcademicTermDto>>(await admin.GetAsync("/api/admin/academic-terms"));
            Assert.Contains(list, term => term.Name == "First term 2027/2028");
            Assert.Contains(list, term => term.Name == "Second term 2027/2028");
            Assert.DoesNotContain(list, term => term.Name == "Overlapping term");
        }

        [Fact]
        public async Task End_date_must_be_after_start_date()
        {
            var admin = await _api.AdminClientAsync();

            var response = await TestApi.PostAsync(admin, "/api/admin/academic-terms", new CreateAcademicTermRequest
            {
                Name = "Backwards term",
                StartDate = new DateOnly(2030, 5, 1),
                EndDate = new DateOnly(2030, 5, 1),
            });

            await TestApi.AssertProblemAsync(response, HttpStatusCode.BadRequest, "AcademicTerm.InvalidDates");
        }

        [Fact]
        public async Task Only_the_platform_admin_can_manage_terms()
        {
            var response = await _api.Anonymous().GetAsync("/api/admin/academic-terms");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
