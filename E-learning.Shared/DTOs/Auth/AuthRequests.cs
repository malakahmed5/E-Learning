using ELearning.Shared.Validation;
using System.ComponentModel.DataAnnotations;

namespace ELearning.Shared.DTOs.Auth
{
    public class LoginRequest
    {
        [Required, EgyptianPhoneNumber]
        public string PhoneNumber { get; set; } = default!;

        [Required, MaxLength(100)]
        public string Password { get; set; } = default!;

        // Sent by the student app. Students can use only one device.
        [MaxLength(100)]
        public string? DeviceId { get; set; }
    }

    public class TwoFactorSetupRequest
    {
        [Required]
        public string ChallengeToken { get; set; } = default!;
    }

    public class TwoFactorVerifyRequest
    {
        [Required]
        public string ChallengeToken { get; set; } = default!;

        // The 6 digits shown in the authenticator app.
        [Required, RegularExpression(@"^\d{6}$", ErrorMessage = "Code must be 6 digits.")]
        public string Code { get; set; } = default!;
    }

    public class RefreshRequest
    {
        [Required]
        public string RefreshToken { get; set; } = default!;

        // The teacher the user wants to work with. Send it to choose or switch teacher.
        public int? TenantId { get; set; }

        [MaxLength(100)]
        public string? DeviceId { get; set; }
    }

    public class LogoutRequest
    {
        [Required]
        public string RefreshToken { get; set; } = default!;
    }

    public class AcceptInviteRequest
    {
        [Required, EgyptianPhoneNumber]
        public string PhoneNumber { get; set; } = default!;

        [Required, MaxLength(20)]
        public string InviteCode { get; set; } = default!;

        [Required, MinLength(8), MaxLength(100)]
        public string Password { get; set; } = default!;
    }
}
