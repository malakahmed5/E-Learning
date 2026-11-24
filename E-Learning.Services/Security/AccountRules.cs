using ELearning.Services.Options;
using ELearningHub.Core.Entities;

namespace ELearning.Services.Security
{
    // The password rule, in one place so every screen that sets a password uses the same rule.
    public static class PasswordRules
    {
        // At least 8 characters, with at least one letter and one digit.
        public static bool IsStrongEnough(string password)
        {
            return password.Length >= 8
                   && password.Any(char.IsLetter)
                   && password.Any(char.IsDigit);
        }
    }

    // Locks an account for a while after too many wrong tries in a row.
    // Used for wrong passwords, wrong two-factor codes, wrong invite codes and wrong activation codes,
    // so none of them can be guessed by trying many values.
    public static class AccountLockout
    {
        public static bool IsLocked(Account account, DateTime now)
        {
            return account.LockoutEndsAt is not null && account.LockoutEndsAt > now;
        }

        // Counts one wrong try. The caller saves the account.
        public static void RegisterFailure(Account account, AuthOptions options, DateTime now)
        {
            account.AccessFailedCount++;

            if (account.AccessFailedCount >= options.MaxFailedAttempts)
            {
                account.LockoutEndsAt = now.AddMinutes(options.LockoutMinutes);
                account.AccessFailedCount = 0;
            }
        }
    }
}
