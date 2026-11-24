using ELearning.Core.Entities.Students;

namespace ELearning.Core.Entities.Subscriptions
{
    // One access window. Rows are only added, never edited, so the full history is kept.
    public class SubscriptionPeriod : BaseEntity<int>
    {
        public int StudentId { get; set; }
        public Student Student { get; set; } = default!;

        public PeriodType Type { get; set; }
        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }
        public PeriodSource Source { get; set; }

        // Id of the code in the hub database (no foreign key: different database).
        public int? HubCodeId { get; set; }
    }
}
