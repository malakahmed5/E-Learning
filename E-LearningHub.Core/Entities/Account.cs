namespace ELearningHub.Core.Entities
{
    // One person, one login. The same account can belong to several teachers (see Membership).
    public class Account : HubEntity
    {
        public string FullName { get; set; } = default!;

        // Unique across the whole platform.
        public string PhoneNumber { get; set; } = default!;

        // Null until the account is activated.
        public string? PasswordHash { get; set; }
        public bool IsActive { get; set; } = true;

        // The platform owner's team.
        public bool IsPlatformAdmin { get; set; }

        // Staff (teacher, assistant, admin) set their first password with a one-time invite code.
        // We store only a hash of the code, never the code itself.
        public string? InviteCodeHash { get; set; }
        public DateTime? InviteExpiresAt { get; set; }

        // Two-factor (teachers & assistants). The secret is stored encrypted.
        public string? TwoFactorSecret { get; set; }
        public DateTime? TwoFactorConfirmedAt { get; set; }

        // The last time step of an accepted two-factor code.
        // Used to stop the same 6-digit code from being used twice.
        public long? TwoFactorLastStep { get; set; }

        // Login lockout.
        public int AccessFailedCount { get; set; }
        public DateTime? LockoutEndsAt { get; set; }

        public ICollection<Membership> Memberships { get; set; } = [];
        public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
        public AccountDevice? Device { get; set; }
    }
}
