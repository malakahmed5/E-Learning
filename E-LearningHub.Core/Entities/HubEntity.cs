namespace ELearningHub.Core.Entities
{
    // CreatedAt / UpdatedAt are filled automatically by the HubDbContext on save (always UTC).
    public abstract class HubEntity
    {
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
