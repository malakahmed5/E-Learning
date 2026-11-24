using ELearning.Infrastructure.Data.DbContexts;
using ELearning.Services.Admin;
using ELearningHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELearning.API.Extensions
{
    // Work done once, when the application starts.
    public static class WebApplicationRegister
    {
        // 1) Updates the hub database.
        // 2) Updates every teacher's database listed in the hub (creates it if it does not exist yet).
        public static async Task<WebApplication> MigrateDatabasesAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            await hub.Database.MigrateAsync();

            var tenants = await hub.Tenants
                .AsNoTracking()
                .Select(t => new { t.Slug, t.ConnectionString })
                .ToListAsync();

            foreach (var tenant in tenants)
            {
                try
                {
                    await using var tenantDatabase = TenantDbContextFactory.Create(tenant.ConnectionString);
                    await tenantDatabase.Database.MigrateAsync();
                }
                catch (Exception exception)
                {
                    // One broken teacher database must not stop the platform for everyone else.
                    logger.LogError(exception, "Failed to update the database of teacher '{Slug}'", tenant.Slug);
                }
            }

            return app;
        }

        // Creates the first platform admin when there is none (see PlatformAdminSeeder).
        public static async Task<WebApplication> SeedPlatformAdminAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var seeder = scope.ServiceProvider.GetRequiredService<PlatformAdminSeeder>();
            await seeder.SeedAsync();
            return app;
        }
    }
}
