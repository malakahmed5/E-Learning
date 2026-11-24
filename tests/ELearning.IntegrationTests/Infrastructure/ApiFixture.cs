using ELearning.API;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace ELearning.IntegrationTests.Infrastructure
{
    // All integration tests share one running copy of the API (starting it is slow).
    [CollectionDefinition(Name)]
    public class ApiCollection : ICollectionFixture<ApiFixture>
    {
        public const string Name = "Api";
    }

    // Starts the real API in memory, connected to real PostgreSQL databases made only for this test run.
    // Every run gets its own database names, and all of them are deleted when the run ends.
    public class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
    {
        public const string AdminPhone = "01000000001";
        public const string AdminPassword = "Admin12345";

        // Settings are given to the API through environment variables, because the API reads
        // some settings very early at startup, before other ways of overriding them are applied.
        private readonly Dictionary<string, string> _settings;
        private readonly string _serverConnectionString;
        private readonly string _databasePrefix;

        public AdjustableClock Clock { get; } = new();

        public ApiFixture()
        {
            // Pooling is off in tests: a test run creates many databases, and keeping idle connections
            // open to each of them would use up all the connections PostgreSQL allows.
            _serverConnectionString = new NpgsqlConnectionStringBuilder(FindPostgresServerConnectionString())
            {
                Pooling = false
            }.ConnectionString;
            _databasePrefix = "eltest_" + Guid.NewGuid().ToString("N").Substring(0, 8) + "_";

            var hubConnectionString = new NpgsqlConnectionStringBuilder(_serverConnectionString)
            {
                Database = _databasePrefix + "hub"
            }.ConnectionString;

            _settings = new Dictionary<string, string>
            {
                ["ConnectionStrings__HubConnection"] = hubConnectionString,
                ["Tenancy__ServerConnectionString"] = _serverConnectionString,
                ["Tenancy__DatabasePrefix"] = _databasePrefix + "t_",
                ["JWTOptions__SecretKey"] = "integration-test-signing-key-long-enough-0123456789",
                ["JWTOptions__Issuer"] = "elearning-tests",
                ["JWTOptions__Audience"] = "elearning-tests",
                ["SeedAdmin__PhoneNumber"] = AdminPhone,
                ["SeedAdmin__Password"] = AdminPassword,
                ["SeedAdmin__FullName"] = "Test Admin",
                // Tests call the login endpoints many times; the limit must not get in the way.
                ["RateLimiting__AuthCallsPerMinute"] = "100000",
            };

            foreach (var setting in _settings)
                Environment.SetEnvironmentVariable(setting.Key, setting.Value);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // "Testing" means appsettings.Development.json is not loaded, so tests never touch the dev databases.
            builder.UseEnvironment("Testing");

            builder.ConfigureTestServices(services =>
            {
                // Replace the real clock with one the tests can move.
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(Clock);
            });
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        // Runs once after all tests: deletes every database this run created.
        async Task IAsyncLifetime.DisposeAsync()
        {
            await base.DisposeAsync();

            foreach (var setting in _settings)
                Environment.SetEnvironmentVariable(setting.Key, null);

            // Close pooled connections first; PostgreSQL refuses to drop a database that is in use.
            NpgsqlConnection.ClearAllPools();

            var adminConnectionString = new NpgsqlConnectionStringBuilder(_serverConnectionString)
            {
                Database = "postgres"
            }.ConnectionString;

            await using var connection = new NpgsqlConnection(adminConnectionString);
            await connection.OpenAsync();

            var databaseNames = new List<string>();
            await using (var listCommand = new NpgsqlCommand("SELECT datname FROM pg_database WHERE datname LIKE @prefix", connection))
            {
                listCommand.Parameters.AddWithValue("prefix", _databasePrefix + "%");
                await using var reader = await listCommand.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    databaseNames.Add(reader.GetString(0));
            }

            foreach (var databaseName in databaseNames)
            {
                // The name comes from PostgreSQL itself and starts with our own prefix, so it is safe to use here.
                await using var dropCommand = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", connection);
                dropCommand.CommandTimeout = 120;
                await dropCommand.ExecuteNonQueryAsync();
            }
        }

        // Where is PostgreSQL? First the ELEARNING_TEST_POSTGRES environment variable (for build servers),
        // then the same server the developer uses (from the API's appsettings.Development.json).
        private static string FindPostgresServerConnectionString()
        {
            var fromEnvironment = Environment.GetEnvironmentVariable("ELEARNING_TEST_POSTGRES");
            if (!string.IsNullOrWhiteSpace(fromEnvironment))
                return fromEnvironment;

            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "E-Learning.sln")))
                directory = directory.Parent;

            if (directory is null)
                throw new InvalidOperationException("Could not find the solution folder to read the PostgreSQL settings.");

            var developmentSettings = new ConfigurationBuilder()
                .AddJsonFile(Path.Combine(directory.FullName, "E-Learning", "appsettings.Development.json"))
                .Build();

            return developmentSettings["Tenancy:ServerConnectionString"]
                ?? throw new InvalidOperationException("Tenancy:ServerConnectionString is missing in appsettings.Development.json.");
        }
    }

    // The real time plus an offset the test can change. Used to test things like "locked for 15 minutes".
    public class AdjustableClock : TimeProvider
    {
        public TimeSpan Offset { get; set; } = TimeSpan.Zero;

        public override DateTimeOffset GetUtcNow()
        {
            return DateTimeOffset.UtcNow + Offset;
        }
    }
}
