namespace ELearningHub.Core.Entities
{
    public class AcademicTerm : HubEntity
    {
        public string Name { get; set; } = default!;
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
    }
}
