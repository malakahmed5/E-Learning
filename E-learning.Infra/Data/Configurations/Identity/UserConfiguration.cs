using ELearning.Core.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ELearning.Infrastructure.Data.Configurations.Identity
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.Property(u => u.FullName).HasMaxLength(150).IsRequired();
            builder.Property(u => u.PhoneNumber).HasMaxLength(15).IsRequired();

            // One row per hub account, and one per phone number, inside this teacher's database.
            builder.HasIndex(u => u.HubAccountId).IsUnique();
            builder.HasIndex(u => u.PhoneNumber).IsUnique();

            builder.ToTable(t => t.HasCheckConstraint("ck_users_role", "role BETWEEN 1 AND 3"));
        }
    }
}
