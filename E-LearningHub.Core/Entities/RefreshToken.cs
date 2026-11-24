namespace ELearningHub.Core.Entities
{
    public class RefreshToken : HubEntity
    {
        public int AccountId { get; set; }
        public Account Account { get; set; } = default!;

        // SHA-256 of the token. The token itself is never stored.
        public string TokenHash { get; set; } = default!;
        public string? DeviceId { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }
    }
}
