using ELearning.Core.Entities.Groups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ELearning.Infrastructure.Data.Configurations.Groups
{
    public class CenterConfiguration : IEntityTypeConfiguration<Center>
    {
        public void Configure(EntityTypeBuilder<Center> builder)
        {
            builder.Property(c => c.Name).HasMaxLength(150).IsRequired();
            builder.Property(c => c.Address).HasMaxLength(300);
        }
    }
}
