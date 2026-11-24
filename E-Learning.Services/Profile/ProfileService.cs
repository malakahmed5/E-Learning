using ELearning.Core.Contracts;
using ELearning.Services.Abstraction;
using ELearning.Shared.CommonResponses;
using ELearning.Shared.DTOs.Auth;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Services.Profile
{
    // Reads the logged-in user's own row from the chosen teacher's database.
    public class ProfileService : IProfileService
    {
        private readonly IELearningDbContext _database;
        private readonly ICurrentUser _currentUser;

        public ProfileService(IELearningDbContext database, ICurrentUser currentUser)
        {
            _database = database;
            _currentUser = currentUser;
        }

        public async Task<Result<TenantProfileDto>> GetMyProfileAsync()
        {
            // The id comes from the verified token, so a user can only ever read their own row.
            var userId = _currentUser.TenantUserId;

            var profile = await _database.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new TenantProfileDto
                {
                    UserId = u.Id,
                    FullName = u.FullName,
                    PhoneNumber = u.PhoneNumber,
                    Role = u.Role.ToString(),
                })
                .FirstOrDefaultAsync();

            if (profile is null)
                return Error.NotFound("Profile.NotFound", "Your profile was not found at this teacher.");

            return profile;
        }
    }
}
