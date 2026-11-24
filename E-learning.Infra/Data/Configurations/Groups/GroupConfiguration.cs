using ELearning.Core.Entities.Groups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ELearning.Infrastructure.Data.Configurations.Groups
{
    public class GroupConfiguration : IEntityTypeConfiguration<Group>
    {
        public void Configure(EntityTypeBuilder<Group> builder)
        {
            builder.Property(g => g.Name).HasMaxLength(150).IsRequired();

            builder.HasOne(g => g.Center)
                   .WithMany(c => c.Groups)
                   .HasForeignKey(g => g.CenterId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(g => g.GradeLevel);

            builder.ToTable(t => t.HasCheckConstraint("ck_groups_grade_level", "grade_level BETWEEN 1 AND 6"));
        }
    }
}
