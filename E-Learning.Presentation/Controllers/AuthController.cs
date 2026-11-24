using ELearning.Services.Abstraction;
using ELearning.Shared.DTOs.Auth;
using ELearning.Shared.DTOs.Codes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ELearning.Presentation.Controllers
{
    // Login endpoints. They are open (no token needed) because this is where tokens are given.
    // To slow down password guessing, all of them are limited to a number of calls per minute.
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public class AuthController : ApiBaseController
    {
        private readonly IAuthService _authService;
        private readonly IActivationService _activationService;

        public AuthController(IAuthService authService, IActivationService activationService)
        {
            _authService = authService;
            _activationService = activationService;
        }

        // A new student opens the account with a code and chooses a password.
        [HttpPost("activate")]
        public async Task<ActionResult<RedeemResultDto>> Activate(ActivateAccountRequest request)
        {
            var result = await _activationService.ActivateAsync(request);
            return HandleResult(result);
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
        {
            var result = await _authService.LoginAsync(request);
            return HandleResult(result);
        }

        [HttpPost("two-factor/setup")]
        public async Task<ActionResult<TwoFactorSetupResponse>> SetupTwoFactor(TwoFactorSetupRequest request)
        {
            var result = await _authService.SetupTwoFactorAsync(request);
            return HandleResult(result);
        }

        [HttpPost("two-factor/verify")]
        public async Task<ActionResult<SessionDto>> VerifyTwoFactor(TwoFactorVerifyRequest request)
        {
            var result = await _authService.VerifyTwoFactorAsync(request);
            return HandleResult(result);
        }

        // Also used to choose or switch teacher: send the wanted TenantId with the refresh token.
        [HttpPost("refresh")]
        public async Task<ActionResult<SessionDto>> Refresh(RefreshRequest request)
        {
            var result = await _authService.RefreshAsync(request);
            return HandleResult(result);
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout(LogoutRequest request)
        {
            var result = await _authService.LogoutAsync(request);
            return HandleResult(result);
        }

        [HttpPost("accept-invite")]
        public async Task<IActionResult> AcceptInvite(AcceptInviteRequest request)
        {
            var result = await _authService.AcceptInviteAsync(request);
            return HandleResult(result);
        }

        // Public information for the teacher's own link (name and logo).
        [HttpGet("tenants/{slug}")]
        public async Task<ActionResult<TenantBrandingDto>> GetTenantBranding(string slug)
        {
            var result = await _authService.GetTenantBrandingAsync(slug);
            return HandleResult(result);
        }
    }

    public static class RateLimitPolicies
    {
        public const string Auth = "auth";
    }
}
