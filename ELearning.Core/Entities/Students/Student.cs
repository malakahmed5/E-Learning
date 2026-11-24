using ELearning.Core.Entities.Groups;
using ELearning.Core.Entities.Identity;
using ELearning.Core.Entities.Subscriptions;

namespace ELearning.Core.Entities.Students
{
    public class Student : BaseEntity<int>
    {
        public int UserId { get; set; }
        public User User { get; set; } = default!;

        public int GroupId { get; set; }
        public Group Group { get; set; } = default!;

        // Parent (no account).
        public string ParentName { get; set; } = default!;
        public string ParentWhatsApp { get; set; } = default!;
        public DateTime? ParentConsentAt { get; set; }
        public bool HideFromHonorBoard { get; set; }

        // Activation & access at this teacher.
        public DateTime? ActivatedAt { get; set; }
        public DateTime? AccessEndsAt { get; set; }

        // Protection against guessing codes.
        public int FailedCodeAttempts { get; set; }
        public DateTime? CodeLockoutEndsAt { get; set; }

        public ICollection<SubscriptionPeriod> SubscriptionPeriods { get; set; } = [];
    }
}
