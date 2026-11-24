using ELearning.Services.Options;
using ELearning.Shared.Validation;
using ELearningHub.Core.Contracts;
using ELearningHub.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ELearning.Services.Admin
{
    // Creates the first platform admin when the platform has none.
    // Without it nobody could log in to register the first teacher.
    // The phone number and password come from configuration ("SeedAdmin"), never from code.
    public class PlatformAdminSeeder
    {
        private readonly IHubDbContext _hub;
        private readonly IPasswordHasher<Account> _passwordHasher;
        private readonly SeedAdminOptions _options;
        private readonly ILogger<PlatformAdminSeeder> _logger;

        public PlatformAdminSeeder(
            IHubDbContext hub,
            IPasswordHasher<Account> passwordHasher,
            IOptions<SeedAdminOptions> options,
            ILogger<PlatformAdminSeeder> logger)
        {
            _hub = hub;
            _passwordHasher = passwordHasher;
            _options = options.Value;
            _logger = logger;
        }

        public async Task SeedAsync()
        {
            if (await _hub.Accounts.AnyAsync(a => a.IsPlatformAdmin))
                return;

            if (string.IsNullOrWhiteSpace(_options.Password)
                || !EgyptianPhone.TryNormalize(_options.PhoneNumber, out var phoneNumber))
            {
                _logger.LogWarning("There is no platform admin and the SeedAdmin settings are missing or not valid.");
                return;
            }

            var admin = new Account
            {
                FullName = string.IsNullOrWhiteSpace(_options.FullName) ? "Platform Admin" : _options.FullName,
                PhoneNumber = phoneNumber,
                IsPlatformAdmin = true,
            };
            admin.PasswordHash = _passwordHasher.HashPassword(admin, _options.Password);

            _hub.Accounts.Add(admin);
            await _hub.SaveChangesAsync();

            // The password is never written to the log.
            _logger.LogInformation("Created the first platform admin with phone number {PhoneNumber}", phoneNumber);
        }
    }
}
