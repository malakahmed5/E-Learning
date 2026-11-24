namespace ELearning.Core.Entities.Groups
{
    // One weekly session. A "Saturday & Tuesday" group has two rows.
    public class GroupSchedule : BaseEntity<int>
    {
        public int GroupId { get; set; }
        public Group Group { get; set; } = default!;

        public DayOfWeek DayOfWeek { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
    }
}
