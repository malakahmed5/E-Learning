using ELearning.Shared.Validation;
using System.ComponentModel.DataAnnotations;

namespace ELearning.Shared.DTOs.Admin
{
    public class CreateTenantRequest
    {
        // The name in the teacher's link. Small English letters, digits and dashes only.
        [Required, RegularExpression(@"^[a-z0-9]([a-z0-9-]{1,48})[a-z0-9]$",
            ErrorMessage = "Slug must be 3 to 50 characters: small English letters, digits and dashes.")]
        public string Slug { get; set; } = default!;

        // The teacher's name as students see it.
        [Required, MinLength(2), MaxLength(150)]
        public string DisplayName { get; set; } = default!;

        [Required, MinLength(2), MaxLength(150)]
        public string TeacherFullName { get; set; } = default!;

        [Required, EgyptianPhoneNumber]
        public string TeacherPhoneNumber { get; set; } = default!;
    }

    public class TenantDto
    {
        public int Id { get; set; }
        public string Slug { get; set; } = default!;
        public string DisplayName { get; set; } = default!;
        public string? LogoUrl { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? TeacherFullName { get; set; }
        public string? TeacherPhoneNumber { get; set; }
    }

    public class CreateTenantResponse
    {
        public TenantDto Tenant { get; set; } = default!;

        // The one-time code the teacher uses to set a password.
        // Null when the teacher already had an account with a password.
        public InviteDto? Invite { get; set; }
    }

    public class InviteDto
    {
        // Shown only once. We do not store it, so it cannot be shown again.
        public string Code { get; set; } = default!;
        public DateTime ExpiresAt { get; set; }
    }

    public class SetTenantStatusRequest
    {
        public bool IsActive { get; set; }
    }

    public class CreateAcademicTermRequest
    {
        [Required, MinLength(2), MaxLength(100)]
        public string Name { get; set; } = default!;

        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
    }

    public class AcademicTermDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
    }
}
