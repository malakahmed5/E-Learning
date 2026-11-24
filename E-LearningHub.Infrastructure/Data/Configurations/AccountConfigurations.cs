using ELearningHub.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ELearningHub.Infrastructure.Data.Configurations
{
    public class AccountConfiguration : IEntityTypeConfiguration<Account>
    {
        public void Configure(EntityTypeBuilder<Account> builder)
        {
            builder.Property(a => a.FullName).HasMaxLength(150).IsRequired();
            builder.Property(a => a.PhoneNumber).HasMaxLength(15).IsRequired();
            builder.Property(a => a.PasswordHash).HasMaxLength(500);
            builder.Property(a => a.TwoFactorSecret).HasMaxLength(500);
            builder.Property(a => a.InviteCodeHash).HasMaxLength(64).IsFixedLength();

            // One login per phone number on the whole platform.
            builder.HasIndex(a => a.PhoneNumber).IsUnique();
        }
    }

    public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            builder.Property(t => t.TokenHash).HasMaxLength(64).IsFixedLength().IsRequired();
            builder.Property(t => t.DeviceId).HasMaxLength(100);

            builder.HasOne(t => t.Account)
                   .WithMany(a => a.RefreshTokens)
                   .HasForeignKey(t => t.AccountId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(t => t.TokenHash).IsUnique();
        }
    }

    public class AccountDeviceConfiguration : IEntityTypeConfiguration<AccountDevice>
    {
        public void Configure(EntityTypeBuilder<AccountDevice> builder)
        {
            // The primary key is the account => one device per account.
            builder.HasKey(d => d.AccountId);
            builder.Property(d => d.AccountId).ValueGeneratedNever();
            builder.Property(d => d.DeviceId).HasMaxLength(100).IsRequired();

            builder.HasOne(d => d.Account)
                   .WithOne(a => a.Device)
                   .HasForeignKey<AccountDevice>(d => d.AccountId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class DeviceResetConfiguration : IEntityTypeConfiguration<DeviceReset>
    {
        public void Configure(EntityTypeBuilder<DeviceReset> builder)
        {
            builder.HasOne(r => r.Account)
                   .WithMany()
                   .HasForeignKey(r => r.AccountId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(r => r.AcademicTerm)
                   .WithMany()
                   .HasForeignKey(r => r.AcademicTermId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(r => r.ResetInTenant)
                   .WithMany()
                   .HasForeignKey(r => r.ResetInTenantId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(r => r.ResetByAccount)
                   .WithMany()
                   .HasForeignKey(r => r.ResetByAccountId)
                   .OnDelete(DeleteBehavior.Restrict);

            // "Max 2 per term" is counted through this index.
            builder.HasIndex(r => new { r.AccountId, r.AcademicTermId });
        }
    }
}
