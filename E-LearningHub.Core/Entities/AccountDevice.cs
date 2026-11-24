namespace ELearningHub.Core.Entities
{
    // The single device a student account is bound to. AccountId is the primary key => one row per account.
    public class AccountDevice
    {
        public int AccountId { get; set; }
        public Account Account { get; set; } = default!;

        public string DeviceId { get; set; } = default!;
        public DateTime BoundAt { get; set; }
    }

    // One row each time an account's device is unbound. CreatedAt is the reset time. Max 2 per term.
    public class DeviceReset : HubEntity
    {
        public int AccountId { get; set; }
        public Account Account { get; set; } = default!;

        public int AcademicTermId { get; set; }
        public AcademicTerm AcademicTerm { get; set; } = default!;

        // Which teacher's staff did it, and who.
        public int ResetInTenantId { get; set; }
        public Tenant ResetInTenant { get; set; } = default!;

        public int ResetByAccountId { get; set; }
        public Account ResetByAccount { get; set; } = default!;
    }
}
