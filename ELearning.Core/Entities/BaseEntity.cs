namespace ELearning.Core.Entities
{
    public interface IAuditableEntity
    {
        DateTime CreatedAt { get; set; }
        DateTime? UpdatedAt { get; set; }
    }

    // CreatedAt / UpdatedAt are filled automatically by the DbContext on save (always UTC).
    public abstract class BaseEntity<TKey> : IAuditableEntity
    {
        public TKey Id { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
