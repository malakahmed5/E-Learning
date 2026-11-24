using ELearning.Shared.CommonResponses;

namespace ELearning.Services.Auth
{
    // Every error the login flow can return, in one place.
    // The code (first text) is stable: the frontend uses it to show an Arabic or English message.
    public static class AuthErrors
    {
        // One answer for "unknown phone" and "wrong password".
        // Giving different answers would tell an attacker which phone numbers have accounts.
        public static Error InvalidCredentials => Error.InvalidCredentials(
            "Auth.InvalidCredentials", "The phone number or password is not correct.");

        public static Error AccountDisabled => Error.Forbidden(
            "Auth.AccountDisabled", "This account is disabled.");

        public static Error AccountLocked => Error.Forbidden(
            "Auth.AccountLocked", "Too many wrong tries. The account is locked for a while.");

        public static Error InvalidChallenge => Error.UnAuthorized(
            "Auth.InvalidChallenge", "The login step has expired. Please log in again.");

        public static Error InvalidTwoFactorCode => Error.InvalidCredentials(
            "Auth.InvalidTwoFactorCode", "The verification code is not correct.");

        public static Error TwoFactorAlreadySetUp => Error.Conflict(
            "Auth.TwoFactorAlreadySetUp", "Two-factor is already set up for this account.");

        public static Error TwoFactorNotSetUp => Error.Conflict(
            "Auth.TwoFactorNotSetUp", "Two-factor is not set up yet for this account.");

        public static Error DeviceRequired => Error.Validation(
            "Auth.DeviceRequired", "A device id is required for student accounts.");

        public static Error DeviceMismatch => Error.Forbidden(
            "Auth.DeviceMismatch", "This account is linked to another device. Ask the assistant to unlock it.");

        public static Error InvalidRefreshToken => Error.UnAuthorized(
            "Auth.InvalidRefreshToken", "The session is no longer valid. Please log in again.");

        public static Error TenantNotAvailable => Error.Forbidden(
            "Auth.TenantNotAvailable", "You cannot open this teacher's platform.");

        public static Error InvalidInvite => Error.InvalidCredentials(
            "Auth.InvalidInvite", "The phone number or invite code is not correct, or the code has expired.");

        public static Error WeakPassword => Error.Validation(
            "Auth.WeakPassword", "The password must be at least 8 characters and contain a letter and a digit.");

        public static Error TenantNotFound => Error.NotFound(
            "Tenant.NotFound", "This teacher was not found.");
    }
}
