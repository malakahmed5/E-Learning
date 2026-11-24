namespace ELearningHub.Core.Entities
{
    public enum MembershipRole
    {
        Teacher = 1,
        Assistant = 2,
        Student = 3,
    }

    // "This account is with this teacher". A student with two teachers has two rows.
    // After login, these rows are the list of teachers shown to the user.
    public class Membership : HubEntity
    {
        public int AccountId { get; set; }
        public Account Account { get; set; } = default!;

        public int TenantId { get; set; }
        public Tenant Tenant { get; set; } = default!;

        public MembershipRole Role { get; set; }

        // Id of this person's row in the teacher's own database (users table).
        public int TenantUserId { get; set; }

        // False = suspended at this teacher only.
        public bool IsActive { get; set; } = true;
    }
}
