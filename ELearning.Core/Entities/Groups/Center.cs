namespace ELearning.Core.Entities.Groups
{
    public class Center : BaseEntity<int>
    {
        public string Name { get; set; } = default!;
        public string? Address { get; set; }

        public ICollection<Group> Groups { get; set; } = [];
    }
}
