namespace ELearning.Shared.Security
{
    // Names of the values we put inside the login token (JWT).
    // Kept in one place so the code that writes a token and the code that reads it always agree.
    public static class AppClaims
    {
        // Id of the account in the hub database.
        public const string AccountId = "sub";

        // "true" when the account belongs to the platform team.
        public const string IsPlatformAdmin = "adm";

        // The teacher the user chose after login. Missing when no teacher is chosen yet.
        public const string TenantId = "tenant_id";

        // Id of the user's row inside that teacher's database.
        public const string TenantUserId = "tenant_user_id";

        // Teacher, Assistant or Student (at the chosen teacher).
        public const string Role = "role";
    }

    public static class AppRoles
    {
        public const string Teacher = "Teacher";
        public const string Assistant = "Assistant";
        public const string Student = "Student";

        // Teacher and assistant together.
        public const string Staff = Teacher + "," + Assistant;
    }

    public static class AppPolicies
    {
        // Only the platform team.
        public const string PlatformAdmin = "PlatformAdmin";

        // Any logged-in user who has chosen a teacher.
        public const string TenantMember = "TenantMember";
    }
}
