using ELearning.Services.Abstraction;
using ELearning.Shared.DTOs.Admin;
using ELearning.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELearning.Presentation.Controllers.Admin
{
    // Platform team only: register and manage teachers.
    [Authorize(Policy = AppPolicies.PlatformAdmin)]
    [Route("api/admin/tenants")]
    public class TenantsController : ApiBaseController
    {
        private readonly ITenantAdminService _tenantAdminService;

        public TenantsController(ITenantAdminService tenantAdminService)
        {
            _tenantAdminService = tenantAdminService;
        }

        [HttpGet]
        public async Task<ActionResult<List<TenantDto>>> List()
        {
            return Ok(await _tenantAdminService.ListAsync());
        }

        [HttpPost]
        public async Task<ActionResult<CreateTenantResponse>> Create(CreateTenantRequest request)
        {
            var result = await _tenantAdminService.CreateAsync(request);
            return HandleResult(result);
        }

        [HttpPut("{tenantId:int}/status")]
        public async Task<IActionResult> SetStatus(int tenantId, SetTenantStatusRequest request)
        {
            var result = await _tenantAdminService.SetStatusAsync(tenantId, request.IsActive);
            return HandleResult(result);
        }

        [HttpPost("{tenantId:int}/teacher-invite")]
        public async Task<ActionResult<InviteDto>> ResetTeacherInvite(int tenantId)
        {
            var result = await _tenantAdminService.ResetTeacherInviteAsync(tenantId);
            return HandleResult(result);
        }
    }
}
