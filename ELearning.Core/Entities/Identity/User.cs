using ELearning.Core.Entities.Students;

namespace ELearning.Core.Entities.Identity
{
    // A person inside this teacher's database: the teacher, an assistant or a student.
    // Login data (password, two-factor) is NOT here: it lives in the hub's Account.
    public class User : BaseEntity<int>
    {
        // Id of this person's account in the hub database (no foreign key: different database).
        public int HubAccountId { get; set; }

        public UserRole Role { get; set; }
        public string FullName { get; set; } = default!;
        public string PhoneNumber { get; set; } = default!;

        // False = suspended at this teacher.
        public bool IsActive { get; set; } = true;

        public Student? Student { get; set; }
    }
}
