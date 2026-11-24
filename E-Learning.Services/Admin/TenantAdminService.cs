using ELearning.Core.Contracts;
using ELearning.Core.Entities.Identity;
using ELearning.Services.Abstraction;
using ELearning.Services.Options;
using ELearning.Services.Security;
using ELearning.Shared.CommonResponses;
using ELearning.Shared.DTOs.Admin;
using ELearning.Shared.Validation;
using ELearningHub.Core.Contracts;
using ELearningHub.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ELearning.Services.Admin
{
    // Platform team only: registers teachers.
    // Registering a teacher touches two databases: the hub (the teacher's record and login account)
    // and a brand-new database that will hold this teacher's own data.
    public class TenantAdminService : ITenantAdminService
    {
        // Link names we keep for the platform itself.
        private static readonly string[] ReservedSlugs = ["admin", "api", "app", "www", "login", "hub", "support"];

        private readonly IHubDbContext _hub;
        private readonly ITenantDatabaseManager _tenantDatabases;
        private readonly AuthOptions _authOptions;
        private readonly TimeProvider _clock;
        private readonly ILogger<TenantAdminService> _logger;

        public TenantAdminService(
            IHubDbContext hub,
            ITenantDatabaseManager tenantDatabases,
            IOptions<AuthOptions> authOptions,
            TimeProvider clock,
            ILogger<TenantAdminService> logger)
        {
            _hub = hub;
            _tenantDatabases = tenantDatabases;
            _authOptions = authOptions.Value;
            _clock = clock;
            _logger = logger;
        }

        public async Task<Result<CreateTenantResponse>> CreateAsync(CreateTenantRequest request)
        {
            var slug = request.Slug.Trim().ToLowerInvariant();

            if (ReservedSlugs.Contains(slug))
                return Error.Validation("Tenant.SlugReserved", "This link name is reserved. Choose another one.");

            if (!EgyptianPhone.TryNormalize(request.TeacherPhoneNumber, out var teacherPhone))
                return Error.Validation("Tenant.InvalidPhone", "The teacher's phone number is not valid.");

            if (await _hub.Tenants.AnyAsync(t => t.Slug == slug))
                return Error.Conflict("Tenant.SlugTaken", "Another teacher already uses this link name.");

            // Step 1: create the teacher's own database.
            // Done first because it is the step most likely to fail (server down, no permission).
            // If it fails, nothing was saved in the hub yet.
            var connectionString = _tenantDatabases.BuildConnectionString(slug);
            try
            {
                await _tenantDatabases.CreateOrUpdateDatabaseAsync(connectionString);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not create the database for teacher '{Slug}'", slug);
                return Error.Failure("Tenant.DatabaseCreationFailed", "The teacher's database could not be created.");
            }

            // Step 2: save the teacher and the login account in the hub.
            // The teacher stays turned off until every step is done.
            var account = await FindOrCreateTeacherAccountAsync(teacherPhone, request.TeacherFullName.Trim());

            // A person who already has a password (for example a teacher of another subject) needs no invite.
            InviteDto? invite = null;
            if (account.PasswordHash is null)
                invite = AccountInvite.Start(account, UtcNow(), _authOptions.InviteValidDays);

            var tenant = new Tenant
            {
                Slug = slug,
                DisplayName = request.DisplayName.Trim(),
                ConnectionString = connectionString,
                IsActive = false,
            };
            _hub.Tenants.Add(tenant);
            await _hub.SaveChangesAsync();

            try
            {
                // Step 3: add the teacher as the first person inside the new database.
                var teacherUserId = await AddTeacherToOwnDatabaseAsync(connectionString, account);

                // Step 4: link the account to the teacher's platform and turn the platform on.
                _hub.Memberships.Add(new Membership
                {
                    AccountId = account.Id,
                    TenantId = tenant.Id,
                    Role = MembershipRole.Teacher,
                    TenantUserId = teacherUserId,
                });
                tenant.IsActive = true;
                await _hub.SaveChangesAsync();
            }
            catch (Exception exception)
            {
                // The two databases cannot be saved in one transaction, so we undo by hand:
                // remove the half-registered teacher so the admin can simply try again.
                _logger.LogError(exception, "Registering teacher '{Slug}' failed after the hub record was saved", slug);
                _hub.Tenants.Remove(tenant);
                await _hub.SaveChangesAsync();
                return Error.Failure("Tenant.RegistrationFailed", "The teacher could not be registered. Please try again.");
            }

            return new CreateTenantResponse
            {
                Tenant = ToDto(tenant, account),
                Invite = invite,
            };
        }

        public async Task<List<TenantDto>> ListAsync()
        {
            var tenants = await _hub.Tenants
                .AsNoTracking()
                .Include(t => t.Memberships.Where(m => m.Role == MembershipRole.Teacher))
                    .ThenInclude(m => m.Account)
                .OrderBy(t => t.DisplayName)
                .ToListAsync();

            var result = new List<TenantDto>();
            foreach (var tenant in tenants)
            {
                var teacherAccount = tenant.Memberships.FirstOrDefault()?.Account;
                result.Add(ToDto(tenant, teacherAccount));
            }
            return result;
        }

        public async Task<Result> SetStatusAsync(int tenantId, bool isActive)
        {
            var tenant = await _hub.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
            if (tenant is null)
                return Error.NotFound("Tenant.NotFound", "This teacher was not found.");

            // Turning a teacher off blocks every request to that teacher's platform at once,
            // because each request checks this flag (see TenantResolutionMiddleware).
            tenant.IsActive = isActive;
            await _hub.SaveChangesAsync();
            return Result.Ok();
        }

        public async Task<Result<InviteDto>> ResetTeacherInviteAsync(int tenantId)
        {
            var teacherAccount = await _hub.Memberships
                .Where(m => m.TenantId == tenantId && m.Role == MembershipRole.Teacher)
                .Select(m => m.Account)
                .FirstOrDefaultAsync();

            if (teacherAccount is null)
                return Error.NotFound("Tenant.NotFound", "This teacher was not found.");

            // The new code lets the teacher set a new password. It replaces any older code.
            var invite = AccountInvite.Start(teacherAccount, UtcNow(), _authOptions.InviteValidDays);
            await _hub.SaveChangesAsync();
            return invite;
        }

        private async Task<Account> FindOrCreateTeacherAccountAsync(string phoneNumber, string fullName)
        {
            var account = await _hub.Accounts.FirstOrDefaultAsync(a => a.PhoneNumber == phoneNumber);
            if (account is not null)
                return account;

            account = new Account
            {
                FullName = fullName,
                PhoneNumber = phoneNumber,
            };
            _hub.Accounts.Add(account);
            return account;
        }

        // Returns the id of the teacher's row inside the teacher's own database.
        private async Task<int> AddTeacherToOwnDatabaseAsync(string connectionString, Account account)
        {
            await using var tenantDatabase = _tenantDatabases.Open(connectionString);

            // If an earlier try already added this person, reuse the row instead of failing.
            var existingUser = await tenantDatabase.Users.FirstOrDefaultAsync(u => u.HubAccountId == account.Id);
            if (existingUser is not null)
                return existingUser.Id;

            var user = new User
            {
                HubAccountId = account.Id,
                Role = UserRole.Teacher,
                FullName = account.FullName,
                PhoneNumber = account.PhoneNumber,
            };
            tenantDatabase.Users.Add(user);
            await tenantDatabase.SaveChangesAsync();
            return user.Id;
        }

        private static TenantDto ToDto(Tenant tenant, Account? teacherAccount)
        {
            return new TenantDto
            {
                Id = tenant.Id,
                Slug = tenant.Slug,
                DisplayName = tenant.DisplayName,
                LogoUrl = tenant.LogoUrl,
                IsActive = tenant.IsActive,
                CreatedAt = tenant.CreatedAt,
                TeacherFullName = teacherAccount?.FullName,
                TeacherPhoneNumber = teacherAccount?.PhoneNumber,
            };
        }

        private DateTime UtcNow()
        {
            return _clock.GetUtcNow().UtcDateTime;
        }
    }
}
