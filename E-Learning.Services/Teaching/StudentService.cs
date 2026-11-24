using ELearning.Core.Contracts;
using ELearning.Core.Entities.Auditing;
using ELearning.Core.Entities.Identity;
using ELearning.Core.Entities.Students;
using ELearning.Core.Entities.Subscriptions;
using ELearning.Services.Abstraction;
using ELearning.Services.Codes;
using ELearning.Shared.CommonResponses;
using ELearning.Shared.DTOs;
using ELearning.Shared.DTOs.Teaching;
using ELearning.Shared.Security;
using ELearning.Shared.Validation;
using ELearningHub.Core.Contracts;
using ELearningHub.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ELearning.Services.Teaching
{
    // The teacher's students: adding, listing, editing, suspending and unlocking a device.
    //
    // A student lives in two databases:
    //   - the hub has the login account and the link "this account is a student of this teacher";
    //   - the teacher's database has the student's data (group, parent, ...).
    // The two cannot be saved in one transaction, so every method that touches both
    // saves in a careful order and undoes its own work if a later step fails.
    public class StudentService : IStudentService
    {
        // Business rule: a student's device can be changed at most twice per term.
        private const int DeviceResetLimitPerTerm = 2;

        // The teacher sees a warning for students whose days end within this many days.
        private const int ExpiringSoonDays = 3;

        private readonly IELearningDbContext _database;
        private readonly IHubDbContext _hub;
        private readonly ICurrentUser _currentUser;
        private readonly TimeProvider _clock;
        private readonly ILogger<StudentService> _logger;

        public StudentService(
            IELearningDbContext database,
            IHubDbContext hub,
            ICurrentUser currentUser,
            TimeProvider clock,
            ILogger<StudentService> logger)
        {
            _database = database;
            _hub = hub;
            _currentUser = currentUser;
            _clock = clock;
            _logger = logger;
        }

        public async Task<PaginatedResult<StudentDto>> ListAsync(StudentQuery query)
        {
            var students = _database.Students.AsNoTracking();

            if (query.GroupId is not null)
                students = students.Where(s => s.GroupId == query.GroupId);

            if (query.GradeLevel is not null)
            {
                var gradeLevel = (Core.Entities.Groups.GradeLevel)query.GradeLevel.Value;
                students = students.Where(s => s.Group.GradeLevel == gradeLevel);
            }

            if (query.Subscription is not null)
            {
                var now = UtcNow();
                var soon = now.AddDays(ExpiringSoonDays);

                // "Expired" = had days and they are over. "Expiring soon" = still has days, but 3 or fewer.
                students = query.Subscription == SubscriptionFilter.Expired
                    ? students.Where(s => s.AccessEndsAt != null && s.AccessEndsAt <= now)
                    : students.Where(s => s.AccessEndsAt != null && s.AccessEndsAt > now && s.AccessEndsAt <= soon);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                // Lower-case both sides so "ahmed" finds "Ahmed".
                var search = query.Search.Trim().ToLower();
                students = students.Where(s =>
                    s.User.FullName.ToLower().Contains(search) || s.User.PhoneNumber.Contains(search));
            }

            var totalCount = await students.CountAsync();

            var page = await students
                .OrderBy(s => s.User.FullName)
                .ThenBy(s => s.Id)
                .Skip((query.PageIndex - 1) * query.PageSize)
                .Take(query.PageSize)
                .Include(s => s.User)
                .Include(s => s.Group)
                .ToListAsync();

            var latestPeriodTypes = await LoadLatestPeriodTypesAsync(page.Select(s => s.Id).ToList());

            var items = new List<StudentDto>();
            foreach (var student in page)
            {
                latestPeriodTypes.TryGetValue(student.Id, out var latestPeriodType);
                items.Add(ToDto(student, latestPeriodType));
            }
            return new PaginatedResult<StudentDto>(totalCount, query.PageSize, query.PageIndex, items);
        }

        public async Task<Result<StudentDto>> GetAsync(int studentId)
        {
            var student = await FindWithDetailsAsync(studentId);
            if (student is null)
                return TeachingErrors.StudentNotFound;

            var latestPeriodTypes = await LoadLatestPeriodTypesAsync([student.Id]);
            latestPeriodTypes.TryGetValue(student.Id, out var latestPeriodType);
            return ToDto(student, latestPeriodType);
        }

        // For each student: is their newest period a free week or paid days?
        // One query for the whole page, instead of one query per student.
        private async Task<Dictionary<int, PeriodType?>> LoadLatestPeriodTypesAsync(List<int> studentIds)
        {
            var periods = await _database.SubscriptionPeriods
                .AsNoTracking()
                .Where(p => studentIds.Contains(p.StudentId))
                .OrderBy(p => p.EndsAt)
                .Select(p => new { p.StudentId, p.Type })
                .ToListAsync();

            // The list is sorted oldest first, so the last value written for a student is the newest one.
            var latest = new Dictionary<int, PeriodType?>();
            foreach (var period in periods)
                latest[period.StudentId] = period.Type;

            return latest;
        }

        public async Task<Result<StudentDto>> CreateAsync(CreateStudentRequest request)
        {
            if (!EgyptianPhone.TryNormalize(request.PhoneNumber, out var phoneNumber)
                || !EgyptianPhone.TryNormalize(request.ParentWhatsApp, out var parentWhatsApp))
                return TeachingErrors.InvalidPhone;

            var group = await _database.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == request.GroupId);
            if (group is null)
                return TeachingErrors.GroupNotFound;
            if (!group.IsActive)
                return TeachingErrors.GroupInactive;

            if (await _database.Users.AnyAsync(u => u.PhoneNumber == phoneNumber))
                return TeachingErrors.PhoneTaken;

            var fullName = request.FullName.Trim();
            var tenantId = _currentUser.TenantId!.Value;

            // Step 1 (hub): the login account. If the person already studies with another teacher,
            // the same account is reused, so they keep one login for all their teachers.
            var account = await HubAccounts.FindOrCreateAsync(_hub, phoneNumber, fullName);
            await _hub.SaveChangesAsync();

            // Step 2 (teacher's database): the student's own data.
            var user = new User
            {
                HubAccountId = account.Id,
                Role = UserRole.Student,
                FullName = fullName,
                PhoneNumber = phoneNumber,
            };
            var student = new Student
            {
                User = user,
                GroupId = group.Id,
                ParentName = request.ParentName.Trim(),
                ParentWhatsApp = parentWhatsApp,
                ParentConsentAt = request.ParentConsent ? UtcNow() : null,
                HideFromHonorBoard = request.HideFromHonorBoard,
            };
            _database.Students.Add(student);
            await _database.SaveChangesAsync();

            // Step 3 (hub): the link that lets this account open this teacher's platform.
            try
            {
                _hub.Memberships.Add(new Membership
                {
                    AccountId = account.Id,
                    TenantId = tenantId,
                    Role = MembershipRole.Student,
                    TenantUserId = user.Id,
                });
                await _hub.SaveChangesAsync();
            }
            catch (Exception exception)
            {
                // Undo step 2, so the teacher does not see a student who can never log in.
                _logger.LogError(exception, "Linking a new student to tenant {TenantId} failed", tenantId);
                _database.Students.Remove(student);
                _database.Users.Remove(user);
                await _database.SaveChangesAsync();
                return TeachingErrors.RegistrationFailed;
            }

            return await GetAsync(student.Id);
        }

        public async Task<Result<StudentDto>> UpdateAsync(int studentId, UpdateStudentRequest request)
        {
            var student = await _database.Students
                .Include(s => s.User)
                .Include(s => s.Group)
                .FirstOrDefaultAsync(s => s.Id == studentId);
            if (student is null)
                return TeachingErrors.StudentNotFound;

            if (request.GroupId != student.GroupId)
            {
                var newGroup = await _database.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == request.GroupId);
                if (newGroup is null)
                    return TeachingErrors.GroupNotFound;
                if (!newGroup.IsActive)
                    return TeachingErrors.GroupInactive;

                // Business rule: a student moves only between groups of the same grade.
                if (newGroup.GradeLevel != student.Group.GradeLevel)
                    return TeachingErrors.GroupGradeMismatch;

                student.GroupId = newGroup.Id;
            }

            // An empty value means "keep the saved number" (assistants see it hidden and cannot resend it).
            if (!string.IsNullOrWhiteSpace(request.ParentWhatsApp))
            {
                if (!EgyptianPhone.TryNormalize(request.ParentWhatsApp, out var parentWhatsApp))
                    return TeachingErrors.InvalidPhone;

                student.ParentWhatsApp = parentWhatsApp;
            }

            student.User.FullName = request.FullName.Trim();
            student.ParentName = request.ParentName.Trim();
            student.HideFromHonorBoard = request.HideFromHonorBoard;

            // Keep the first consent date. Removing consent clears it; giving it again records a new date.
            if (!request.ParentConsent)
                student.ParentConsentAt = null;
            else if (student.ParentConsentAt is null)
                student.ParentConsentAt = UtcNow();

            await _database.SaveChangesAsync();
            return await GetAsync(studentId);
        }

        public async Task<Result> SetActiveAsync(int studentId, bool isActive)
        {
            var student = await _database.Students
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Id == studentId);
            if (student is null)
                return TeachingErrors.StudentNotFound;

            // The hub link is what actually blocks access, so it is changed first.
            // If the second save failed, the student would be blocked but still shown as active,
            // which is the safe side to fail on.
            await HubAccounts.SetMembershipActiveAsync(_hub, student.User.HubAccountId, _currentUser.TenantId!.Value, isActive);
            await _hub.SaveChangesAsync();

            student.User.IsActive = isActive;
            await _database.SaveChangesAsync();
            return Result.Ok();
        }

        public async Task<Result<DeviceResetDto>> ResetDeviceAsync(int studentId)
        {
            // Looking the student up in the teacher's own database also proves
            // the student belongs to this teacher. Staff cannot unlock someone else's student.
            var student = await _database.Students
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Id == studentId);
            if (student is null)
                return TeachingErrors.StudentNotFound;

            var accountId = student.User.HubAccountId;
            var today = DateOnly.FromDateTime(UtcNow());

            var currentTerm = await _hub.AcademicTerms
                .FirstOrDefaultAsync(t => t.StartDate <= today && today <= t.EndDate);
            if (currentTerm is null)
                return TeachingErrors.NoCurrentTerm;

            var usedThisTerm = await _hub.DeviceResets
                .CountAsync(r => r.AccountId == accountId && r.AcademicTermId == currentTerm.Id);
            if (usedThisTerm >= DeviceResetLimitPerTerm)
                return TeachingErrors.DeviceLimitReached;

            var device = await _hub.AccountDevices.FirstOrDefaultAsync(d => d.AccountId == accountId);
            if (device is null)
                return TeachingErrors.DeviceNotLinked;

            // Unlink the device: the next device the student logs in from becomes the linked one.
            _hub.AccountDevices.Remove(device);
            _hub.DeviceResets.Add(new DeviceReset
            {
                AccountId = accountId,
                AcademicTermId = currentTerm.Id,
                ResetInTenantId = _currentUser.TenantId!.Value,
                ResetByAccountId = _currentUser.AccountId,
            });

            // End the sessions of the old device, so it cannot keep working with its saved login.
            var openTokens = await _hub.RefreshTokens
                .Where(t => t.AccountId == accountId && t.RevokedAt == null)
                .ToListAsync();
            foreach (var token in openTokens)
                token.RevokedAt = UtcNow();

            await _hub.SaveChangesAsync();

            // The device lives in the hub, so the automatic activity log of the teacher's database
            // does not see this action. We add the log row by hand for assistants.
            if (_currentUser.Role == AppRoles.Assistant)
            {
                _database.AuditLogs.Add(new AuditLog
                {
                    UserId = _currentUser.TenantUserId!.Value,
                    Action = "DeviceReset",
                    EntityName = nameof(Student),
                    EntityId = student.Id.ToString(),
                });
                await _database.SaveChangesAsync();
            }

            return new DeviceResetDto
            {
                UsedInCurrentTerm = usedThisTerm + 1,
                Limit = DeviceResetLimitPerTerm,
            };
        }

        private Task<Student?> FindWithDetailsAsync(int studentId)
        {
            return _database.Students
                .AsNoTracking()
                .Include(s => s.User)
                .Include(s => s.Group)
                .FirstOrDefaultAsync(s => s.Id == studentId);
        }

        private StudentDto ToDto(Student student, PeriodType? latestPeriodType)
        {
            var subscription = SubscriptionRules.GetStatus(student.AccessEndsAt, latestPeriodType, UtcNow(), ExpiringSoonDays);

            // Privacy rule: an assistant never sees a parent's full phone number.
            var parentWhatsApp = _currentUser.Role == AppRoles.Assistant
                ? EgyptianPhone.Mask(student.ParentWhatsApp)
                : student.ParentWhatsApp;

            return new StudentDto
            {
                Id = student.Id,
                FullName = student.User.FullName,
                PhoneNumber = student.User.PhoneNumber,
                GroupId = student.GroupId,
                GroupName = student.Group.Name,
                GradeLevel = (int)student.Group.GradeLevel,
                ParentName = student.ParentName,
                ParentWhatsApp = parentWhatsApp,
                ParentConsent = student.ParentConsentAt is not null,
                HideFromHonorBoard = student.HideFromHonorBoard,
                IsActive = student.User.IsActive,
                IsActivated = student.ActivatedAt is not null,
                Subscription = subscription.Status,
                AccessEndsAt = student.AccessEndsAt,
                ExpiringSoon = subscription.ExpiringSoon,
            };
        }

        private DateTime UtcNow()
        {
            return _clock.GetUtcNow().UtcDateTime;
        }
    }
}
