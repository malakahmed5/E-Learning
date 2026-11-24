using ELearning.Core.Contracts;
using ELearning.Core.Entities.Groups;
using ELearning.Services.Abstraction;
using ELearning.Shared.CommonResponses;
using ELearning.Shared.DTOs.Teaching;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Services.Teaching
{
    // The centers (places) where the teacher gives lessons. A center is only a name and an address.
    public class CenterService : ICenterService
    {
        private readonly IELearningDbContext _database;

        public CenterService(IELearningDbContext database)
        {
            _database = database;
        }

        public async Task<List<CenterDto>> ListAsync()
        {
            return await _database.Centers
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new CenterDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Address = c.Address,
                    GroupCount = c.Groups.Count,
                })
                .ToListAsync();
        }

        public async Task<Result<CenterDto>> CreateAsync(SaveCenterRequest request)
        {
            var name = request.Name.Trim();

            if (await _database.Centers.AnyAsync(c => c.Name == name))
                return TeachingErrors.CenterNameTaken;

            var center = new Center
            {
                Name = name,
                Address = CleanOptionalText(request.Address),
            };
            _database.Centers.Add(center);
            await _database.SaveChangesAsync();

            return ToDto(center, groupCount: 0);
        }

        public async Task<Result<CenterDto>> UpdateAsync(int centerId, SaveCenterRequest request)
        {
            var center = await _database.Centers.FirstOrDefaultAsync(c => c.Id == centerId);
            if (center is null)
                return TeachingErrors.CenterNotFound;

            var name = request.Name.Trim();

            if (await _database.Centers.AnyAsync(c => c.Name == name && c.Id != centerId))
                return TeachingErrors.CenterNameTaken;

            center.Name = name;
            center.Address = CleanOptionalText(request.Address);
            await _database.SaveChangesAsync();

            var groupCount = await _database.Groups.CountAsync(g => g.CenterId == centerId);
            return ToDto(center, groupCount);
        }

        public async Task<Result> DeleteAsync(int centerId)
        {
            var center = await _database.Centers.FirstOrDefaultAsync(c => c.Id == centerId);
            if (center is null)
                return TeachingErrors.CenterNotFound;

            // A center that still has groups cannot be deleted: the groups would lose their place.
            if (await _database.Groups.AnyAsync(g => g.CenterId == centerId))
                return TeachingErrors.CenterHasGroups;

            _database.Centers.Remove(center);
            await _database.SaveChangesAsync();
            return Result.Ok();
        }

        private static string? CleanOptionalText(string? text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        private static CenterDto ToDto(Center center, int groupCount)
        {
            return new CenterDto
            {
                Id = center.Id,
                Name = center.Name,
                Address = center.Address,
                GroupCount = groupCount,
            };
        }
    }
}
