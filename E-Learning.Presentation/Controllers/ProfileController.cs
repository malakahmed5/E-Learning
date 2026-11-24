using ELearning.Services.Abstraction;
using ELearning.Shared.DTOs.Auth;
using ELearning.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELearning.Presentation.Controllers
{
    // The logged-in user's own data at the chosen teacher.
    [Authorize(Policy = AppPolicies.TenantMember)]
    public class ProfileController : ApiBaseController
    {
        private readonly IProfileService _profileService;

        public ProfileController(IProfileService profileService)
        {
            _profileService = profileService;
        }

        [HttpGet]
        public async Task<ActionResult<TenantProfileDto>> GetMyProfile()
        {
            var result = await _profileService.GetMyProfileAsync();
            return HandleResult(result);
        }
    }
}
