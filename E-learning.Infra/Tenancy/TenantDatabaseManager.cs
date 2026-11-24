using ELearning.Core.Contracts;
using ELearning.Infrastructure.Data.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace ELearning.Infrastructure.Tenancy
{
    public class TenancyOptions
    {
        // Connection string of the PostgreSQL server where teacher databases are created.
        // The database name inside it is ignored: each teacher gets a new name.
        public string ServerConnectionString { get; set; } = default!;

        // Every teacher database name starts with this text.
        public string DatabasePrefix { get; set; } = "elearning_t_";
    }

    public class TenantDatabaseManager : ITenantDatabaseManager
    {
        private readonly TenancyOptions _options;

        public TenantDatabaseManager(IOptions<TenancyOptions> options)
        {
            _options = options.Value;
        }

        public string BuildConnectionString(string slug)
        {
            // The slug is already validated (small letters, digits, dashes).
            // PostgreSQL names read better with underscores, so "mr-ahmed" becomes "elearning_t_mr_ahmed".
            var databaseName = _options.DatabasePrefix + slug.Replace('-', '_');

            var builder = new NpgsqlConnectionStringBuilder(_options.ServerConnectionString)
            {
                Database = databaseName,

                // Every teacher database has its own group of open connections.
                // With many teachers this adds up, and PostgreSQL only allows a limited number.
                // So: keep no idle connections, close unused ones after a minute, and cap each teacher.
                MinPoolSize = 0,
                MaxPoolSize = 20,
                ConnectionIdleLifetime = 60,
            };
            return builder.ConnectionString;
        }

        public async Task CreateOrUpdateDatabaseAsync(string connectionString, CancellationToken cancellationToken = default)
        {
            await using var database = TenantDbContextFactory.Create(connectionString);

            // Migrate creates the database when it is missing, then creates or updates the tables.
            await database.Database.MigrateAsync(cancellationToken);
        }

        public IELearningDbContext Open(string connectionString)
        {
            return TenantDbContextFactory.Create(connectionString);
        }
    }
}
