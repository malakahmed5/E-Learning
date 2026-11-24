using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ELearning.Infrastructure.Data.DbContexts
{
    // The one place that knows how a teacher database is configured.
    public static class TenantDbContextFactory
    {
        public static void Configure(DbContextOptionsBuilder builder, string connectionString)
            => builder.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();

        // For work outside a request (e.g. updating every teacher's database at startup).
        public static ELearningDbContext Create(string connectionString)
        {
            var builder = new DbContextOptionsBuilder<ELearningDbContext>();
            Configure(builder, connectionString);
            return new ELearningDbContext(builder.Options);
        }
    }

    // Used only by "dotnet ef migrations add": it needs a context but never connects.
    public class ELearningDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ELearningDbContext>
    {
        public ELearningDbContext CreateDbContext(string[] args)
            => TenantDbContextFactory.Create("Host=localhost;Database=elearning_design_time;Username=postgres");
    }
}
