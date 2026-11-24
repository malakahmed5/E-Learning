namespace ELearning.Core.Contracts
{
    // Tells the database layer who is making the changes of the current request,
    // so it can write the activity log by itself.
    public interface IAuditActor
    {
        // Id of the user's row in the teacher's database. Null when nobody is logged in.
        int? UserId { get; }

        // True when the changes of this user must be recorded.
        // Business rule: everything an assistant does is recorded, so the teacher can review it.
        bool ShouldAudit { get; }
    }
}
