namespace ELearning.Core.Contracts
{
    // The teacher this request belongs to. Each teacher has a separate database.
    public interface ITenantProvider
    {
        // Id of the teacher in the hub database. Null when the request has no teacher.
        int? TenantId { get; }
        string? Slug { get; }
        string? ConnectionString { get; }
    }
}
