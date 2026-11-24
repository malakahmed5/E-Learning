namespace ELearningHub.Core.Entities
{
    public enum CodeType
    {
        // 7 free days. It is also the code a new student uses to open the account for the first time.
        FreeWeek = 1,

        // 30 days of platform access.
        Subscription30 = 2,
    }

    // What is stored. "Expired" is not stored: a code is expired when it is still Available
    // but the last day to redeem it (on its batch) has passed.
    public enum CodeStatus
    {
        Available = 1,
        Used = 2,
        Voided = 3,
    }

    // Codes are generated in groups ("batches"). Settings shared by all codes of a group live here.
    public class CodeBatch : HubEntity
    {
        public CodeType Type { get; set; }
        public int Count { get; set; }

        // How many days of access one code gives.
        // Stored per batch so old codes keep their duration if the default changes later.
        public int DurationDays { get; set; }

        // Optional last moment a code of this batch can be redeemed.
        // This is NOT the access duration: a code redeemed one day before this date still gives all its days.
        public DateTime? RedeemBefore { get; set; }

        public string? Note { get; set; }

        public int CreatedByAccountId { get; set; }
        public Account CreatedByAccount { get; set; } = default!;

        public ICollection<Code> Codes { get; set; } = [];
    }

    // One code. It is not tied to a teacher until a student redeems it.
    public class Code : HubEntity
    {
        public int BatchId { get; set; }
        public CodeBatch Batch { get; set; } = default!;

        // 12 characters, stored without dashes.
        public string Value { get; set; } = default!;
        public CodeStatus Status { get; set; } = CodeStatus.Available;

        public int? UsedByAccountId { get; set; }
        public Account? UsedByAccount { get; set; }

        public int? UsedInTenantId { get; set; }
        public Tenant? UsedInTenant { get; set; }

        public DateTime? UsedAt { get; set; }

        // Concurrency token: two students redeeming the same code at once => only one succeeds.
        public uint Version { get; set; }
    }

    // A record of sensitive actions on the platform level (codes generated, redeemed, revoked, ...).
    // Rows are only added, never changed.
    public class HubAuditLog : HubEntity
    {
        // Who did it. Null only for actions done by the system itself.
        public int? AccountId { get; set; }
        public Account? Account { get; set; }

        // For example "CodesGenerated", "CodeRedeemed", "CodeRevoked".
        public string Action { get; set; } = default!;

        // What it was done to, for example "CodeBatch" with id "12".
        public string EntityName { get; set; } = default!;
        public string EntityId { get; set; } = default!;

        // Short JSON text with extra facts. Never contains a secret.
        public string? Details { get; set; }
    }
}
