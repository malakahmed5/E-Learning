namespace ELearning.Core.Contracts
{
    // Creates and opens teacher databases outside a normal request.
    // Normal requests do not need this: they get the current teacher's database automatically.
    // It is used when the platform admin registers a new teacher.
    public interface ITenantDatabaseManager
    {
        // Builds the connection string of the database that belongs to this teacher link name.
        string BuildConnectionString(string slug);

        // Creates the database if it does not exist and brings its tables up to date.
        Task CreateOrUpdateDatabaseAsync(string connectionString, CancellationToken cancellationToken = default);

        // Opens a teacher database directly. The caller must dispose it (use "await using").
        IELearningDbContext Open(string connectionString);
    }
}
