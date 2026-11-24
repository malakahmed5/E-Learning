using ELearningHub.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ELearningHub.Infrastructure.Data.Configurations
{
    public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
    {
        public void Configure(EntityTypeBuilder<Tenant> builder)
        {
            builder.Property(t => t.Slug).HasMaxLength(50).IsRequired();
            builder.Property(t => t.DisplayName).HasMaxLength(150).IsRequired();
            builder.Property(t => t.LogoUrl).HasMaxLength(500);
            builder.Property(t => t.ConnectionString).HasMaxLength(500).IsRequired();

            builder.HasIndex(t => t.Slug).IsUnique();
        }
    }

    public class MembershipConfiguration : IEntityTypeConfiguration<Membership>
    {
        public void Configure(EntityTypeBuilder<Membership> builder)
        {
            builder.HasOne(m => m.Account)
                   .WithMany(a => a.Memberships)
                   .HasForeignKey(m => m.AccountId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(m => m.Tenant)
                   .WithMany(t => t.Memberships)
                   .HasForeignKey(m => m.TenantId)
                   .OnDelete(DeleteBehavior.Restrict);

            // An account appears once per teacher.
            builder.HasIndex(m => new { m.AccountId, m.TenantId }).IsUnique();

            builder.ToTable(t => t.HasCheckConstraint("ck_memberships_role", "role BETWEEN 1 AND 3"));
        }
    }

    public class AcademicTermConfiguration : IEntityTypeConfiguration<AcademicTerm>
    {
        public void Configure(EntityTypeBuilder<AcademicTerm> builder)
        {
            builder.Property(t => t.Name).HasMaxLength(100).IsRequired();

            builder.ToTable(t => t.HasCheckConstraint("ck_academic_terms_dates", "end_date > start_date"));
        }
    }
}
