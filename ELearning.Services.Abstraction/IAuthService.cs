using ELearning.Shared.CommonResponses;
using ELearning.Shared.DTOs.Auth;

namespace ELearning.Services.Abstraction
{
    // Login and sessions. Works only with the hub database.
    public interface IAuthService
    {
        // Step 1 of login: phone number and password.
        Task<Result<LoginResponse>> LoginAsync(LoginRequest request);

        // Staff only, first time: creates the secret for the authenticator app.
        Task<Result<TwoFactorSetupResponse>> SetupTwoFactorAsync(TwoFactorSetupRequest request);

        // Staff only, step 2 of login: checks the 6-digit code and starts the session.
        Task<Result<SessionDto>> VerifyTwoFactorAsync(TwoFactorVerifyRequest request);

        // Gives a new session from a refresh token. Also used to choose or switch teacher.
        Task<Result<SessionDto>> RefreshAsync(RefreshRequest request);

        Task<Result> LogoutAsync(LogoutRequest request);

        // Staff set their first password with the one-time invite code.
        Task<Result> AcceptInviteAsync(AcceptInviteRequest request);

        // Public: the teacher's name and logo for the teacher's own link.
        Task<Result<TenantBrandingDto>> GetTenantBrandingAsync(string slug);
    }

    // Reads the user's own data inside the chosen teacher's database.
    public interface IProfileService
    {
        Task<Result<TenantProfileDto>> GetMyProfileAsync();
    }
}
