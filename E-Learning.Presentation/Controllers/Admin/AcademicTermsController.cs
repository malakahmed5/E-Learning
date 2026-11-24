using ELearning.Services.Abstraction;
using ELearning.Shared.DTOs.Admin;
using ELearning.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELearning.Presentation.Controllers.Admin
{
    // Platform team only: school term dates.
    [Authorize(Policy = AppPolicies.PlatformAdmin)]
    [Route("api/admin/academic-terms")]
    public class AcademicTermsController : ApiBaseController
    {
        private readonly IAcademicTermService _academicTermService;

        public AcademicTermsController(IAcademicTermService academicTermService)
        {
            _academicTermService = academicTermService;
        }

        [HttpGet]
        public async Task<ActionResult<List<AcademicTermDto>>> List()
        {
            return Ok(await _academicTermService.ListAsync());
        }

        [HttpPost]
        public async Task<ActionResult<AcademicTermDto>> Create(CreateAcademicTermRequest request)
        {
            var result = await _academicTermService.CreateAsync(request);
            return HandleResult(result);
        }
    }
}
