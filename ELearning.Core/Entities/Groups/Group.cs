using ELearning.Core.Entities.Students;

namespace ELearning.Core.Entities.Groups
{
    public class Group : BaseEntity<int>
    {
        public string Name { get; set; } = default!;
        public GradeLevel GradeLevel { get; set; }

        // Null = online group.
        public int? CenterId { get; set; }
        public Center? Center { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<GroupSchedule> Schedules { get; set; } = [];
        public ICollection<Student> Students { get; set; } = [];
    }
}
