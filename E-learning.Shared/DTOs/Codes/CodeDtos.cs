using ELearning.Shared.Validation;
using System.ComponentModel.DataAnnotations;

namespace ELearning.Shared.DTOs.Codes
{
    // The two kinds of codes the platform owner can generate.
    public enum CodeKind
    {
        // 7 free days. A new student also uses it to open the account for the first time.
        FreeWeek = 1,

        // 30 days of platform access.
        Subscription30 = 2,
    }

    // What the owner sees. "Expired" means: never used, and the last day to redeem it has passed.
    public enum CodeState
    {
        Available,
        Redeemed,
        Expired,
        Revoked,
    }

    // ---------- Platform owner ----------

    public class GenerateCodesRequest
    {
        [EnumDataType(typeof(CodeKind))]
        public CodeKind Type { get; set; }

        // One code or many in one batch. The upper limit keeps a single request small.
        [Range(1, 2000)]
        public int Count { get; set; }

        // Optional last moment a code can be redeemed. Empty means the codes never expire.
        public DateTime? RedeemBefore { get; set; }

        [MaxLength(200)]
        public string? Note { get; set; }
    }

    public class GeneratedBatchDto
    {
        public int BatchId { get; set; }
        public CodeKind Type { get; set; }
        public int DurationDays { get; set; }
        public DateTime? RedeemBefore { get; set; }

        // The new codes, written with dashes (XXXX-XXXX-XXXX), ready to copy or export.
        public List<string> Codes { get; set; } = [];
    }

    public class CodeQuery
    {
        public CodeKind? Type { get; set; }
        public CodeState? Status { get; set; }
        public int? BatchId { get; set; }

        // Part of a code, or part of the phone number of the student who redeemed it.
        [MaxLength(50)]
        public string? Search { get; set; }

        [Range(1, int.MaxValue)]
        public int PageIndex { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 20;
    }

    public class CodeDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = default!;
        public CodeKind Type { get; set; }
        public int DurationDays { get; set; }
        public CodeState Status { get; set; }
        public int BatchId { get; set; }
        public string? BatchNote { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? RedeemBefore { get; set; }

        // Filled only for redeemed codes.
        public string? RedeemedByName { get; set; }
        public string? RedeemedByPhone { get; set; }
        public string? RedeemedAtTeacher { get; set; }
        public DateTime? RedeemedAt { get; set; }
    }

    // Numbers for one code type, shown as cards on the owner's page.
    public class CodeStatsDto
    {
        public CodeKind Type { get; set; }
        public int Total { get; set; }
        public int Available { get; set; }
        public int Redeemed { get; set; }
        public int Expired { get; set; }
        public int Revoked { get; set; }
    }

    public class PlatformAuditLogQuery
    {
        [Range(1, int.MaxValue)]
        public int PageIndex { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 20;
    }

    public class PlatformAuditLogDto
    {
        public int Id { get; set; }
        public string? AccountName { get; set; }
        public string Action { get; set; } = default!;
        public string EntityName { get; set; } = default!;
        public string EntityId { get; set; } = default!;
        public string? Details { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ---------- Student ----------

    public class RedeemCodeRequest
    {
        [Required, MaxLength(30)]
        public string Code { get; set; } = default!;
    }

    // A new student opens the account: the code proves they were given access, and they choose a password.
    public class ActivateAccountRequest
    {
        [Required, EgyptianPhoneNumber]
        public string PhoneNumber { get; set; } = default!;

        [Required, MaxLength(30)]
        public string Code { get; set; } = default!;

        [Required, MinLength(8), MaxLength(100)]
        public string Password { get; set; } = default!;

        // The teacher's link name. Needed only when the student was added by more than one teacher.
        [MaxLength(50)]
        public string? TenantSlug { get; set; }
    }

    // Shown to the student right after a code is accepted.
    public class RedeemResultDto
    {
        public CodeKind Type { get; set; }
        public int DurationDays { get; set; }

        // When the new days begin. If the student still had days left, the new days are added after them.
        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }
    }

    public enum SubscriptionState
    {
        // The student never redeemed a code at this teacher.
        NotActivated,

        // Inside the free week.
        Trial,

        // Inside paid days.
        Active,

        // All days are used up. Only the "enter a code" screen is available.
        Expired,
    }

    public class SubscriptionStatusDto
    {
        public SubscriptionState Status { get; set; }
        public DateTime? EndsAt { get; set; }
        public int DaysLeft { get; set; }

        // True in the last 3 days, so the app can remind the student to renew.
        public bool ExpiringSoon { get; set; }
    }
}
