using ELearning.Core.Entities.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ELearning.Infrastructure.Data.Configurations.Subscriptions
{
    public class SubscriptionPeriodConfiguration : IEntityTypeConfiguration<SubscriptionPeriod>
    {
        public void Configure(EntityTypeBuilder<SubscriptionPeriod> builder)
        {
            builder.HasOne(p => p.Student)
                   .WithMany(s => s.SubscriptionPeriods)
                   .HasForeignKey(p => p.StudentId)
                   .OnDelete(DeleteBehavior.Restrict);

            // A code creates at most one period.
            builder.HasIndex(p => p.HubCodeId).IsUnique();
            builder.HasIndex(p => new { p.StudentId, p.EndsAt });

            builder.ToTable(t => t.HasCheckConstraint("ck_subscription_periods_dates", "ends_at > starts_at"));
        }
    }
}
