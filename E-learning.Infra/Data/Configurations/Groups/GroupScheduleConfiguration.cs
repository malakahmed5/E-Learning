using ELearning.Core.Entities.Groups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ELearning.Infrastructure.Data.Configurations.Groups
{
    public class GroupScheduleConfiguration : IEntityTypeConfiguration<GroupSchedule>
    {
        public void Configure(EntityTypeBuilder<GroupSchedule> builder)
        {
            // The only cascade in the model: a schedule has no meaning without its group.
            builder.HasOne(s => s.Group)
                   .WithMany(g => g.Schedules)
                   .HasForeignKey(s => s.GroupId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(s => new { s.GroupId, s.DayOfWeek, s.StartTime }).IsUnique();

            builder.ToTable(t =>
            {
                t.HasCheckConstraint("ck_group_schedules_day_of_week", "day_of_week BETWEEN 0 AND 6");
                t.HasCheckConstraint("ck_group_schedules_times", "end_time > start_time");
            });
        }
    }
}
