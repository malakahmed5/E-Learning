using ELearning.Core.Entities.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ELearning.Infrastructure.Data.Configurations.Students
{
    public class StudentConfiguration : IEntityTypeConfiguration<Student>
    {
        public void Configure(EntityTypeBuilder<Student> builder)
        {
            builder.Property(s => s.ParentName).HasMaxLength(150).IsRequired();
            builder.Property(s => s.ParentWhatsApp).HasMaxLength(15).IsRequired();

            builder.HasOne(s => s.User)
                   .WithOne(u => u.Student)
                   .HasForeignKey<Student>(s => s.UserId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(s => s.Group)
                   .WithMany(g => g.Students)
                   .HasForeignKey(s => s.GroupId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(s => s.UserId).IsUnique();
            builder.HasIndex(s => s.AccessEndsAt);
        }
    }
}
