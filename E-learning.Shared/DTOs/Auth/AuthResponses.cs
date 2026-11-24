namespace ELearning.Shared.DTOs.Auth
{
    public enum LoginStatus
    {
        // Login is finished. The session is in the response.
        Authenticated,

        // Password was correct. Now send the 6-digit code from the authenticator app.
        TwoFactorRequired,

        // Password was correct, but this staff account has no authenticator app yet. Set it up first.
        TwoFactorSetupRequired,
    }

    public class LoginResponse
    {
        public LoginStatus Status { get; set; }

        // Short-lived token that proves the password step passed. Only for the two-factor endpoints.
        public string? ChallengeToken { get; set; }

        public SessionDto? Session { get; set; }
    }

    public class SessionDto
    {
        public string AccessToken { get; set; } = default!;
        public DateTime AccessTokenExpiresAt { get; set; }
        public string RefreshToken { get; set; } = default!;

        public AccountDto Account { get; set; } = default!;

        // The teachers this account belongs to. Shown to the user to choose from.
        public List<MembershipDto> Memberships { get; set; } = [];

        // The teacher this session works with. Null when the user still has to choose.
        public MembershipDto? ActiveMembership { get; set; }
    }

    public class AccountDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = default!;
        public string PhoneNumber { get; set; } = default!;
        public bool IsPlatformAdmin { get; set; }
    }

    public class MembershipDto
    {
        public int TenantId { get; set; }
        public string TenantSlug { get; set; } = default!;
        public string TenantName { get; set; } = default!;
        public string? TenantLogoUrl { get; set; }

        // Teacher, Assistant or Student.
        public string Role { get; set; } = default!;
    }

    public class TwoFactorSetupResponse
    {
        // The secret to type into the authenticator app by hand.
        public string Secret { get; set; } = default!;

        // The same secret as a link. The frontend shows it as a QR code.
        public string OtpAuthUri { get; set; } = default!;
    }

    // Public information about a teacher, shown on the teacher's own link before login.
    public class TenantBrandingDto
    {
        public string Slug { get; set; } = default!;
        public string DisplayName { get; set; } = default!;
        public string? LogoUrl { get; set; }
    }

    // The user's own row inside the chosen teacher's database.
    public class TenantProfileDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = default!;
        public string PhoneNumber { get; set; } = default!;
        public string Role { get; set; } = default!;
    }
}
