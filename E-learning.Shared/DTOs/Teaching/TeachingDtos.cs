using ELearning.Shared.Validation;
using System.ComponentModel.DataAnnotations;

namespace ELearning.Shared.DTOs.Teaching
{
    // ---------- Shared small requests ----------

    // Turns something on or off (a group, a student, an assistant).
    public class SetActiveRequest
    {
        public bool IsActive { get; set; }
    }

    // ---------- Centers ----------

    public class SaveCenterRequest
    {
        [Required, MinLength(2), MaxLength(150)]
        public string Name { get; set; } = default!;

        [MaxLength(300)]
        public string? Address { get; set; }
    }

    public class CenterDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Address { get; set; }
        public int GroupCount { get; set; }
    }

    // ---------- Groups ----------

    public class ScheduleDto
    {
        // Sunday = 0 ... Saturday = 6 (sent as text, for example "Saturday").
        public DayOfWeek DayOfWeek { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
    }

    public class SaveGroupRequest
    {
        [Required, MinLength(2), MaxLength(150)]
        public string Name { get; set; } = default!;

        // 1 = first preparatory ... 6 = third secondary.
        [Range(1, 6)]
        public int GradeLevel { get; set; }

        // Empty means the group is online.
        public int? CenterId { get; set; }

        // The weekly sessions. A group needs at least one, and at most one per day and start time.
        [Required, MinLength(1), MaxLength(14)]
        public List<ScheduleDto> Schedules { get; set; } = [];
    }

    public class GroupDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public int GradeLevel { get; set; }
        public int? CenterId { get; set; }
        public string? CenterName { get; set; }
        public bool IsActive { get; set; }
        public int StudentCount { get; set; }
        public List<ScheduleDto> Schedules { get; set; } = [];
    }

    // ---------- Students ----------

    public class CreateStudentRequest
    {
        [Required, MinLength(2), MaxLength(150)]
        public string FullName { get; set; } = default!;

        [Required, EgyptianPhoneNumber]
        public string PhoneNumber { get; set; } = default!;

        [Range(1, int.MaxValue)]
        public int GroupId { get; set; }

        [Required, MinLength(2), MaxLength(150)]
        public string ParentName { get; set; } = default!;

        [Required, EgyptianPhoneNumber]
        public string ParentWhatsApp { get; set; } = default!;

        // True when the parent agreed to receive WhatsApp reports. Without it no message is ever sent.
        public bool ParentConsent { get; set; }

        // The parent can ask that the student does not appear on the honor board.
        public bool HideFromHonorBoard { get; set; }
    }

    public class UpdateStudentRequest
    {
        [Required, MinLength(2), MaxLength(150)]
        public string FullName { get; set; } = default!;

        [Range(1, int.MaxValue)]
        public int GroupId { get; set; }

        [Required, MinLength(2), MaxLength(150)]
        public string ParentName { get; set; } = default!;

        // Leave empty to keep the saved number. Assistants see the number hidden,
        // so they cannot send it back; an empty value means "no change".
        [EgyptianPhoneNumber]
        public string? ParentWhatsApp { get; set; }

        public bool ParentConsent { get; set; }
        public bool HideFromHonorBoard { get; set; }
    }

    public class StudentQuery
    {
        // Part of the name or the phone number.
        [MaxLength(100)]
        public string? Search { get; set; }

        public int? GroupId { get; set; }

        [Range(1, 6)]
        public int? GradeLevel { get; set; }

        // Show only students whose days are over, or will be over within 3 days.
        public SubscriptionFilter? Subscription { get; set; }

        [Range(1, int.MaxValue)]
        public int PageIndex { get; set; } = 1;

        // The upper limit stops someone from asking for the whole table in one call.
        [Range(1, 100)]
        public int PageSize { get; set; } = 20;
    }

    public class StudentDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = default!;
        public string PhoneNumber { get; set; } = default!;
        public int GroupId { get; set; }
        public string GroupName { get; set; } = default!;
        public int GradeLevel { get; set; }
        public string ParentName { get; set; } = default!;

        // Shown in full to the teacher, and with hidden digits to an assistant.
        public string ParentWhatsApp { get; set; } = default!;

        public bool ParentConsent { get; set; }
        public bool HideFromHonorBoard { get; set; }
        public bool IsActive { get; set; }

        // True after the student has activated the account at this teacher.
        public bool IsActivated { get; set; }

        // The student's platform subscription at this teacher (free week, paid days, or over).
        public Codes.SubscriptionState Subscription { get; set; }
        public DateTime? AccessEndsAt { get; set; }

        // True in the last 3 days before the days run out.
        public bool ExpiringSoon { get; set; }
    }

    public enum SubscriptionFilter
    {
        Expired,
        ExpiringSoon,
    }

    public class DeviceResetDto
    {
        // How many device changes the student has used in the current term (the limit is 2).
        public int UsedInCurrentTerm { get; set; }
        public int Limit { get; set; }
    }

    // ---------- Assistants ----------

    public class CreateAssistantRequest
    {
        [Required, MinLength(2), MaxLength(150)]
        public string FullName { get; set; } = default!;

        [Required, EgyptianPhoneNumber]
        public string PhoneNumber { get; set; } = default!;
    }

    public class AssistantDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = default!;
        public string PhoneNumber { get; set; } = default!;
        public bool IsActive { get; set; }
    }

    public class CreateAssistantResponse
    {
        public AssistantDto Assistant { get; set; } = default!;

        // The one-time code the assistant uses to set a password.
        // Null when the person already had an account with a password.
        public Admin.InviteDto? Invite { get; set; }
    }

    // ---------- Activity log ----------

    public class AuditLogQuery
    {
        public int? UserId { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }

        [Range(1, int.MaxValue)]
        public int PageIndex { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 20;
    }

    public class AuditLogDto
    {
        public long Id { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = default!;

        // Created, Updated or Deleted.
        public string Action { get; set; } = default!;

        // What was changed, for example "Student".
        public string EntityName { get; set; } = default!;
        public string EntityId { get; set; } = default!;

        // JSON text with the changed values.
        public string? Details { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
