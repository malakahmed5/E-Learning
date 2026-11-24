using ELearning.Core.Contracts;
using ELearning.Core.Entities.Groups;
using ELearning.Services.Abstraction;
using ELearning.Shared.CommonResponses;
using ELearning.Shared.DTOs.Teaching;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Services.Teaching
{
    // Groups of students and their weekly lesson times.
    // The lesson times matter later: homework deadlines are calculated from them.
    public class GroupService : IGroupService
    {
        private readonly IELearningDbContext _database;

        public GroupService(IELearningDbContext database)
        {
            _database = database;
        }

        public async Task<List<GroupDto>> ListAsync(bool includeInactive)
        {
            var groups = await _database.Groups
                .AsNoTracking()
                .Where(g => includeInactive || g.IsActive)
                .Include(g => g.Center)
                .Include(g => g.Schedules)
                .OrderBy(g => g.GradeLevel)
                .ThenBy(g => g.Name)
                .ToListAsync();

            // One extra query gives the number of students in every group.
            var studentCounts = await _database.Students
                .GroupBy(s => s.GroupId)
                .Select(counted => new { GroupId = counted.Key, Count = counted.Count() })
                .ToDictionaryAsync(counted => counted.GroupId, counted => counted.Count);

            var result = new List<GroupDto>();
            foreach (var group in groups)
            {
                studentCounts.TryGetValue(group.Id, out var studentCount);
                result.Add(ToDto(group, studentCount));
            }
            return result;
        }

        public async Task<Result<GroupDto>> GetAsync(int groupId)
        {
            var group = await FindWithDetailsAsync(groupId);
            if (group is null)
                return TeachingErrors.GroupNotFound;

            var studentCount = await _database.Students.CountAsync(s => s.GroupId == groupId);
            return ToDto(group, studentCount);
        }

        public async Task<Result<GroupDto>> CreateAsync(SaveGroupRequest request)
        {
            var problem = await ValidateAsync(request, existingGroupId: null);
            if (problem is not null)
                return problem;

            var group = new Group
            {
                Name = request.Name.Trim(),
                GradeLevel = (GradeLevel)request.GradeLevel,
                CenterId = request.CenterId,
                Schedules = request.Schedules.Select(ToSchedule).ToList(),
            };
            _database.Groups.Add(group);
            await _database.SaveChangesAsync();

            return await GetAsync(group.Id);
        }

        public async Task<Result<GroupDto>> UpdateAsync(int groupId, SaveGroupRequest request)
        {
            var group = await _database.Groups
                .Include(g => g.Schedules)
                .FirstOrDefaultAsync(g => g.Id == groupId);
            if (group is null)
                return TeachingErrors.GroupNotFound;

            var problem = await ValidateAsync(request, existingGroupId: groupId);
            if (problem is not null)
                return problem;

            group.Name = request.Name.Trim();
            group.GradeLevel = (GradeLevel)request.GradeLevel;
            group.CenterId = request.CenterId;

            // Replace the lesson times with the new list.
            _database.GroupSchedules.RemoveRange(group.Schedules);
            group.Schedules = request.Schedules.Select(ToSchedule).ToList();

            await _database.SaveChangesAsync();
            return await GetAsync(groupId);
        }

        public async Task<Result> SetActiveAsync(int groupId, bool isActive)
        {
            var group = await _database.Groups.FirstOrDefaultAsync(g => g.Id == groupId);
            if (group is null)
                return TeachingErrors.GroupNotFound;

            // Turning a group off keeps all of its data. It only hides the group from daily work.
            group.IsActive = isActive;
            await _database.SaveChangesAsync();
            return Result.Ok();
        }

        public async Task<Result> DeleteAsync(int groupId)
        {
            var group = await _database.Groups.FirstOrDefaultAsync(g => g.Id == groupId);
            if (group is null)
                return TeachingErrors.GroupNotFound;

            // Deleting is only for groups made by mistake. A group with students is turned off instead.
            if (await _database.Students.AnyAsync(s => s.GroupId == groupId))
                return TeachingErrors.GroupHasStudents;

            _database.Groups.Remove(group);
            await _database.SaveChangesAsync();
            return Result.Ok();
        }

        // Returns the first problem found in the request, or null when everything is fine.
        private async Task<Error?> ValidateAsync(SaveGroupRequest request, int? existingGroupId)
        {
            foreach (var schedule in request.Schedules)
            {
                if (schedule.EndTime <= schedule.StartTime)
                    return TeachingErrors.ScheduleInvalidTimes;
            }

            var distinctTimes = request.Schedules
                .Select(s => (s.DayOfWeek, s.StartTime))
                .Distinct()
                .Count();
            if (distinctTimes != request.Schedules.Count)
                return TeachingErrors.ScheduleDuplicate;

            if (request.CenterId is not null && !await _database.Centers.AnyAsync(c => c.Id == request.CenterId))
                return TeachingErrors.CenterNotFound;

            var name = request.Name.Trim();
            var gradeLevel = (GradeLevel)request.GradeLevel;
            var nameIsTaken = await _database.Groups.AnyAsync(g =>
                g.Name == name && g.GradeLevel == gradeLevel && g.Id != existingGroupId);
            if (nameIsTaken)
                return TeachingErrors.GroupNameTaken;

            return null;
        }

        private Task<Group?> FindWithDetailsAsync(int groupId)
        {
            return _database.Groups
                .AsNoTracking()
                .Include(g => g.Center)
                .Include(g => g.Schedules)
                .FirstOrDefaultAsync(g => g.Id == groupId);
        }

        private static GroupSchedule ToSchedule(ScheduleDto schedule)
        {
            return new GroupSchedule
            {
                DayOfWeek = schedule.DayOfWeek,
                StartTime = schedule.StartTime,
                EndTime = schedule.EndTime,
            };
        }

        private static GroupDto ToDto(Group group, int studentCount)
        {
            return new GroupDto
            {
                Id = group.Id,
                Name = group.Name,
                GradeLevel = (int)group.GradeLevel,
                CenterId = group.CenterId,
                CenterName = group.Center?.Name,
                IsActive = group.IsActive,
                StudentCount = studentCount,
                Schedules = group.Schedules
                    .OrderBy(s => s.DayOfWeek)
                    .ThenBy(s => s.StartTime)
                    .Select(s => new ScheduleDto
                    {
                        DayOfWeek = s.DayOfWeek,
                        StartTime = s.StartTime,
                        EndTime = s.EndTime,
                    })
                    .ToList(),
            };
        }
    }
}
