using ELearning.Core.Contracts;
using ELearning.Core.Entities.Identity;
using ELearning.Services.Abstraction;
using ELearning.Services.Options;
using ELearning.Services.Security;
using ELearning.Shared.CommonResponses;
using ELearning.Shared.DTOs.Admin;
using ELearning.Shared.DTOs.Teaching;
using ELearning.Shared.Validation;
using ELearningHub.Core.Contracts;
using ELearningHub.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ELearning.Services.Teaching
{
    // Teacher only: the teacher's assistants.
    // An assistant sets their own password with a one-time invite code, like a teacher does.
    public class AssistantService : IAssistantService
    {
        private readonly IELearningDbContext _database;
        private readonly IHubDbContext _hub;
        private readonly ICurrentUser _currentUser;
        private readonly AuthOptions _authOptions;
        private readonly TimeProvider _clock;
        private readonly ILogger<AssistantService> _logger;

        public AssistantService(
            IELearningDbContext database,
            IHubDbContext hub,
            ICurrentUser currentUser,
            IOptions<AuthOptions> authOptions,
            TimeProvider clock,
            ILogger<AssistantService> logger)
        {
            _database = database;
            _hub = hub;
            _currentUser = currentUser;
            _authOptions = authOptions.Value;
            _clock = clock;
            _logger = logger;
        }

        public async Task<List<AssistantDto>> ListAsync()
        {
            return await _database.Users
                .AsNoTracking()
                .Where(u => u.Role == UserRole.Assistant)
                .OrderBy(u => u.FullName)
                .Select(u => new AssistantDto
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    PhoneNumber = u.PhoneNumber,
                    IsActive = u.IsActive,
                })
                .ToListAsync();
        }

        public async Task<Result<CreateAssistantResponse>> CreateAsync(CreateAssistantRequest request)
        {
            if (!EgyptianPhone.TryNormalize(request.PhoneNumber, out var phoneNumber))
                return TeachingErrors.InvalidPhone;

            if (await _database.Users.AnyAsync(u => u.PhoneNumber == phoneNumber))
                return TeachingErrors.PhoneTaken;

            var fullName = request.FullName.Trim();
            var tenantId = _currentUser.TenantId!.Value;

            // Step 1 (hub): the login account, with an invite code when the person has no password yet.
            var account = await HubAccounts.FindOrCreateAsync(_hub, phoneNumber, fullName);

            InviteDto? invite = null;
            if (account.PasswordHash is null)
                invite = AccountInvite.Start(account, _clock.GetUtcNow().UtcDateTime, _authOptions.InviteValidDays);

            await _hub.SaveChangesAsync();

            // Step 2 (teacher's database): the assistant's row.
            var user = new User
            {
                HubAccountId = account.Id,
                Role = UserRole.Assistant,
                FullName = fullName,
                PhoneNumber = phoneNumber,
            };
            _database.Users.Add(user);
            await _database.SaveChangesAsync();

            // Step 3 (hub): the link that lets this account open this teacher's platform.
            try
            {
                _hub.Memberships.Add(new Membership
                {
                    AccountId = account.Id,
                    TenantId = tenantId,
                    Role = MembershipRole.Assistant,
                    TenantUserId = user.Id,
                });
                await _hub.SaveChangesAsync();
            }
            catch (Exception exception)
            {
                // Undo step 2, so the teacher does not see an assistant who can never log in.
                _logger.LogError(exception, "Linking a new assistant to tenant {TenantId} failed", tenantId);
                _database.Users.Remove(user);
                await _database.SaveChangesAsync();
                return TeachingErrors.RegistrationFailed;
            }

            return new CreateAssistantResponse
            {
                Assistant = new AssistantDto
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    PhoneNumber = user.PhoneNumber,
                    IsActive = user.IsActive,
                },
                Invite = invite,
            };
        }

        public async Task<Result> SetActiveAsync(int assistantUserId, bool isActive)
        {
            var assistant = await _database.Users
                .FirstOrDefaultAsync(u => u.Id == assistantUserId && u.Role == UserRole.Assistant);
            if (assistant is null)
                return TeachingErrors.AssistantNotFound;

            // The hub link is what actually blocks access, so it is changed first.
            // A suspended assistant loses access on the very next request.
            await HubAccounts.SetMembershipActiveAsync(_hub, assistant.HubAccountId, _currentUser.TenantId!.Value, isActive);
            await _hub.SaveChangesAsync();

            assistant.IsActive = isActive;
            await _database.SaveChangesAsync();
            return Result.Ok();
        }
    }
}
