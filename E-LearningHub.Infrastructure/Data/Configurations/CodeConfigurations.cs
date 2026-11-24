using ELearningHub.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ELearningHub.Infrastructure.Data.Configurations
{
    public class CodeBatchConfiguration : IEntityTypeConfiguration<CodeBatch>
    {
        public void Configure(EntityTypeBuilder<CodeBatch> builder)
        {
            builder.Property(b => b.Note).HasMaxLength(200);

            builder.HasOne(b => b.CreatedByAccount)
                   .WithMany()
                   .HasForeignKey(b => b.CreatedByAccountId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.ToTable(t =>
            {
                t.HasCheckConstraint("ck_code_batches_count", "\"count\" > 0");
                t.HasCheckConstraint("ck_code_batches_duration_days", "duration_days > 0");
            });
        }
    }

    public class CodeConfiguration : IEntityTypeConfiguration<Code>
    {
        public void Configure(EntityTypeBuilder<Code> builder)
        {
            builder.Property(c => c.Value).HasMaxLength(12).IsFixedLength().IsRequired();

            // Mapped to PostgreSQL's xmin system column, so no extra column is created.
            builder.Property(c => c.Version).IsRowVersion();

            builder.HasOne(c => c.Batch)
                   .WithMany(b => b.Codes)
                   .HasForeignKey(c => c.BatchId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(c => c.UsedByAccount)
                   .WithMany()
                   .HasForeignKey(c => c.UsedByAccountId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(c => c.UsedInTenant)
                   .WithMany()
                   .HasForeignKey(c => c.UsedInTenantId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(c => c.Value).IsUnique();
            builder.HasIndex(c => new { c.BatchId, c.Status });
        }
    }

    public class HubAuditLogConfiguration : IEntityTypeConfiguration<HubAuditLog>
    {
        public void Configure(EntityTypeBuilder<HubAuditLog> builder)
        {
            builder.ToTable("audit_logs");

            builder.Property(a => a.Action).HasMaxLength(50).IsRequired();
            builder.Property(a => a.EntityName).HasMaxLength(100).IsRequired();
            builder.Property(a => a.EntityId).HasMaxLength(50).IsRequired();
            builder.Property(a => a.Details).HasColumnType("jsonb");

            builder.HasOne(a => a.Account)
                   .WithMany()
                   .HasForeignKey(a => a.AccountId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(a => a.CreatedAt);
        }
    }
}