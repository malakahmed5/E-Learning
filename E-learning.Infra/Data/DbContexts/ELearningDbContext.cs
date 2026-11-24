using ELearning.Core.Contracts;
using ELearning.Core.Entities;
using ELearning.Core.Entities.Auditing;
using ELearning.Core.Entities.Groups;
using ELearning.Core.Entities.Identity;
using ELearning.Core.Entities.Students;
using ELearning.Core.Entities.Subscriptions;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace ELearning.Infrastructure.Data.DbContexts
{
    // One teacher's database. Which database it points at is decided per request,
    // from the teacher's connection string stored in the hub.
    public class ELearningDbContext : DbContext, IELearningDbContext
    {
        public ELearningDbContext(DbContextOptions<ELearningDbContext> options) : base(options) { }

        #region DbSets
        public DbSet<User> Users => Set<User>();

        public DbSet<Center> Centers => Set<Center>();
        public DbSet<Group> Groups => Set<Group>();
        public DbSet<GroupSchedule> GroupSchedules => Set<GroupSchedule>();

        public DbSet<Student> Students => Set<Student>();
        public DbSet<SubscriptionPeriod> SubscriptionPeriods => Set<SubscriptionPeriod>();

        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        #endregion

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
            foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
            {
                if (entry.State == EntityState.Added)
                    entry.Entity.CreatedAt = now;
                else if (entry.State == EntityState.Modified)
                    entry.Entity.UpdatedAt = now;
            }
        }
    }
}
