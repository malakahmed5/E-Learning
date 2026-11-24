using ELearningHub.Core.Contracts;
using ELearningHub.Core.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace ELearningHub.Infrastructure.Data
{
    public class HubDbContext : DbContext, IHubDbContext
    {
        public HubDbContext(DbContextOptions<HubDbContext> options) : base(options) { }

        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<Account> Accounts => Set<Account>();
        public DbSet<Membership> Memberships => Set<Membership>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<AccountDevice> AccountDevices => Set<AccountDevice>();
        public DbSet<DeviceReset> DeviceResets => Set<DeviceReset>();
        public DbSet<AcademicTerm> AcademicTerms => Set<AcademicTerm>();
        public DbSet<CodeBatch> CodeBatches => Set<CodeBatch>();
        public DbSet<Code> Codes => Set<Code>();
        public DbSet<HubAuditLog> AuditLogs => Set<HubAuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            ApplyTimestamps();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            ApplyTimestamps();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void ApplyTimestamps()
        {
            var now = DateTime.UtcNow;
            foreach (var entry in ChangeTracker.Entries<HubEntity>())
            {
                if (entry.State == EntityState.Added)
                    entry.Entity.CreatedAt = now;
                else if (entry.State == EntityState.Modified)
                    entry.Entity.UpdatedAt = now;
            }
        }
    }
}
