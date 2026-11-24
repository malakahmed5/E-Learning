namespace ELearning.Services.Abstraction
{
    // Who is calling the API right now. The values come from the verified login token,
    // never from the request body, so the user cannot fake them.
    public interface ICurrentUser
    {
        // Id of the account in the hub database.
        int AccountId { get; }

        bool IsPlatformAdmin { get; }

        // The teacher the user chose. Null when no teacher is chosen.
        int? TenantId { get; }

        // Id of the user's row inside the chosen teacher's database.
        int? TenantUserId { get; }

        // Teacher, Assistant or Student at the chosen teacher.
        string? Role { get; }
    }
}
