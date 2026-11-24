namespace ELearning.Services.Options
{
    // Settings for login tokens. Read from the "JWTOptions" section of the configuration.
    public class JwtOptions
    {
        // The secret used to sign tokens. Must be long and must never be committed to source control.
        public string SecretKey { get; set; } = default!;
        public string Issuer { get; set; } = default!;
        public string Audience { get; set; } = default!;

        // Access tokens are short-lived on purpose: a stolen one stops working quickly.
        public int AccessTokenMinutes { get; set; } = 30;

        public int RefreshTokenDays { get; set; } = 30;

        // How long the user has to finish the two-factor step after the password step.
        public int ChallengeTokenMinutes { get; set; } = 10;
    }

    // Settings for login rules. Read from the "Auth" section of the configuration.
    public class AuthOptions
    {
        // After this many wrong tries in a row, the account is locked for a while.
        public int MaxFailedAttempts { get; set; } = 5;
        public int LockoutMinutes { get; set; } = 15;

        // How long a staff invite code stays valid.
        public int InviteValidDays { get; set; } = 7;

        // The name shown inside the authenticator app next to the code.
        public string TwoFactorIssuer { get; set; } = "E-Learning";
    }

    // The first platform admin, created at startup when there is none. Section "SeedAdmin".
    public class SeedAdminOptions
    {
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Password { get; set; }
    }
}
