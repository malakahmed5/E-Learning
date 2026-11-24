namespace ELearningHub.Core.Entities
{
    // One teacher. Each teacher has a separate E-Learning database; this row says where it is.
    public class Tenant : HubEntity
    {
        public string Slug { get; set; } = default!;
        public string DisplayName { get; set; } = default!;
        public string? LogoUrl { get; set; }
        public bool IsActive { get; set; } = true;

        // Connection string of this teacher's own database.
        public string ConnectionString { get; set; } = default!;

        public ICollection<Membership> Memberships { get; set; } = [];
    }
}
