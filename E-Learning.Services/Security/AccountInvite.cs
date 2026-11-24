using ELearning.Shared.DTOs.Admin;
using ELearningHub.Core.Entities;

namespace ELearning.Services.Security
{
    // Staff do not choose a password when someone else creates their account.
    // They get a one-time invite code and set their own password with it.
    public static class AccountInvite
    {
        // Puts a new invite on the account and returns the code to show once.
        // The caller saves the account.
        public static InviteDto Start(Account account, DateTime now, int validDays)
        {
            var code = SecureCode.Generate();
            var expiresAt = now.AddDays(validDays);

            // Only the hash is stored. The real code exists only in the returned value.
            account.InviteCodeHash = SecureCode.Hash(code);
            account.InviteExpiresAt = expiresAt;

            return new InviteDto
            {
                Code = SecureCode.Format(code),
                ExpiresAt = expiresAt,
            };
        }
    }
}
