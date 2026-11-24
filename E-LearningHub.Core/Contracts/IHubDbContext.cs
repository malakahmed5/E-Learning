using ELearningHub.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ELearningHub.Core.Contracts
{
    // The central database: who can log in, which teachers they belong to,
    // and where each teacher's database is.
    public interface IHubDbContext
    {
        DbSet<Tenant> Tenants { get; }
        DbSet<Account> Accounts { get; }
        DbSet<Membership> Memberships { get; }
        DbSet<RefreshToken> RefreshTokens { get; }
        DbSet<AccountDevice> AccountDevices { get; }
        DbSet<DeviceReset> DeviceResets { get; }
        DbSet<AcademicTerm> AcademicTerms { get; }
        DbSet<CodeBatch> CodeBatches { get; }
        DbSet<Code> Codes { get; }
        DbSet<HubAuditLog> AuditLogs { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
