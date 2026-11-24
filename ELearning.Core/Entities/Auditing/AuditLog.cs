using ELearning.Core.Entities.Identity;

namespace ELearning.Core.Entities.Auditing
{
    // Append-only. CreatedAt is when the action happened.
    public class AuditLog : BaseEntity<long>
    {
        public int UserId { get; set; }
        public User User { get; set; } = default!;

        public string Action { get; set; } = default!;
        public string EntityName { get; set; } = default!;
        public string EntityId { get; set; } = default!;

        // JSON with the values before and after.
        public string? Details { get; set; }
    }
}
