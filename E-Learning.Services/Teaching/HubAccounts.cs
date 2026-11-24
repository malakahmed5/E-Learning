using ELearningHub.Core.Contracts;
using ELearningHub.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Services.Teaching
{
    // Shared steps for linking a person to a teacher.
    // A person has ONE login account in the hub, even when they belong to several teachers.
    internal static class HubAccounts
    {
        // Returns the account with this phone number, or a new one that is added but not saved yet.
        public static async Task<Account> FindOrCreateAsync(IHubDbContext hub, string phoneNumber, string fullName)
        {
            var account = await hub.Accounts.FirstOrDefaultAsync(a => a.PhoneNumber == phoneNumber);
            if (account is not null)
                return account;

            account = new Account
            {
                FullName = fullName,
                PhoneNumber = phoneNumber,
            };
            hub.Accounts.Add(account);
            return account;
        }

        // Turns the link between an account and a teacher on or off.
        // The middleware checks this link on every request, so the change works at once.
        public static async Task SetMembershipActiveAsync(IHubDbContext hub, int accountId, int tenantId, bool isActive)
        {
            var membership = await hub.Memberships
                .FirstOrDefaultAsync(m => m.AccountId == accountId && m.TenantId == tenantId);

            if (membership is not null)
                membership.IsActive = isActive;
        }
    }
}
