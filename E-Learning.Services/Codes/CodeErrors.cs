using ELearning.Shared.CommonResponses;

namespace ELearning.Services.Codes
{
    // Every error the code services can return, in one place.
    // The code (first text) is stable: the website uses it to show an Arabic or English message.
    public static class CodeErrors
    {
        public static Error Invalid => Error.Validation("Code.Invalid", "This code is not correct.");
        public static Error AlreadyUsed => Error.Conflict("Code.AlreadyUsed", "This code was already used.");
        public static Error Expired => Error.Conflict("Code.Expired", "This code has expired.");
        public static Error Revoked => Error.Conflict("Code.Revoked", "This code was cancelled.");

        public static Error FreeWeekAlreadyUsed => Error.Conflict(
            "Code.FreeWeekAlreadyUsed", "You already used a free week with this teacher.");

        public static Error TooManyAttempts => Error.Forbidden(
            "Code.TooManyAttempts", "Too many wrong codes. Please wait a while and try again.");

        public static Error RedeemFailed => Error.Failure(
            "Code.RedeemFailed", "The code could not be applied. It was not used up. Please try again.");

        public static Error NotFound => Error.NotFound("Code.NotFound", "This code was not found.");
        public static Error BatchNotFound => Error.NotFound("Code.BatchNotFound", "This batch was not found.");

        public static Error RedeemBeforeInPast => Error.Validation(
            "Code.RedeemBeforeInPast", "The last day to redeem must be in the future.");

        public static Error StudentNotFound => Error.NotFound(
            "Subscription.StudentNotFound", "Your student record was not found at this teacher.");

        // One answer for "unknown phone" and "not a student anywhere", so phone numbers cannot be probed.
        public static Error ActivationInvalid => Error.InvalidCredentials(
            "Activation.Invalid", "The phone number or the code is not correct.");

        public static Error AlreadyActivated => Error.Conflict(
            "Activation.AlreadyActivated", "This account is already active. Log in with your password.");

        public static Error ChooseTeacher => Error.Validation(
            "Activation.ChooseTeacher", "You are registered with more than one teacher. Open your teacher's link to activate.");
    }
}
