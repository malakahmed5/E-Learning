using ELearning.Services.Abstraction;
using ELearning.Shared.DTOs;
using ELearning.Shared.DTOs.Codes;
using ELearning.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELearning.Presentation.Controllers
{
    // Platform owner only: generate and manage subscription codes.
    [Authorize(Policy = AppPolicies.PlatformAdmin)]
    [Route("api/admin/codes")]
    public class AdminCodesController : ApiBaseController
    {
        private readonly ICodeAdminService _codeAdminService;

        public AdminCodesController(ICodeAdminService codeAdminService)
        {
            _codeAdminService = codeAdminService;
        }

        // Generates one code or a whole batch. The answer has the new codes, ready to copy or export.
        [HttpPost("batches")]
        public async Task<ActionResult<GeneratedBatchDto>> Generate(GenerateCodesRequest request)
        {
            return HandleResult(await _codeAdminService.GenerateAsync(request));
        }

        [HttpGet]
        public async Task<ActionResult<PaginatedResult<CodeDto>>> List([FromQuery] CodeQuery query)
        {
            return Ok(await _codeAdminService.ListAsync(query));
        }

        [HttpGet("stats")]
        public async Task<ActionResult<List<CodeStatsDto>>> Stats()
        {
            return Ok(await _codeAdminService.GetStatsAsync());
        }

        [HttpPost("{codeId:int}/revoke")]
        public async Task<IActionResult> Revoke(int codeId)
        {
            return HandleResult(await _codeAdminService.RevokeAsync(codeId));
        }

        [HttpGet("batches/{batchId:int}/export")]
        public async Task<ActionResult<List<string>>> Export(int batchId)
        {
            return HandleResult(await _codeAdminService.ExportBatchAsync(batchId));
        }

        // The record of sensitive platform actions (codes generated, redeemed, revoked, exported).
        [HttpGet("/api/admin/audit-logs")]
        public async Task<ActionResult<PaginatedResult<PlatformAuditLogDto>>> AuditLog([FromQuery] PlatformAuditLogQuery query)
        {
            return Ok(await _codeAdminService.ListAuditLogAsync(query));
        }
    }

    // A logged-in student at the chosen teacher: see the subscription and enter a code.
    // Teachers and assistants are refused: the platform is free for them and they have no subscription.
    [Authorize(Roles = AppRoles.Student)]
    public class SubscriptionController : ApiBaseController
    {
        private readonly ISubscriptionService _subscriptionService;

        public SubscriptionController(ISubscriptionService subscriptionService)
        {
            _subscriptionService = subscriptionService;
        }

        [HttpGet]
        public async Task<ActionResult<SubscriptionStatusDto>> GetMyStatus()
        {
            return HandleResult(await _subscriptionService.GetMyStatusAsync());
        }

        [HttpPost("redeem")]
        public async Task<ActionResult<RedeemResultDto>> Redeem(RedeemCodeRequest request)
        {
            return HandleResult(await _subscriptionService.RedeemAsync(request));
        }
    }
}
