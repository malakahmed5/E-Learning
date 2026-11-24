using ELearning.Shared.CommonResponses;
using ELearning.Shared.DTOs;
using ELearning.Shared.DTOs.Teaching;

namespace ELearning.Services.Abstraction
{
    // Everything here works inside the chosen teacher's own database.

    // The places where the teacher gives lessons.
    public interface ICenterService
    {
        Task<List<CenterDto>> ListAsync();
        Task<Result<CenterDto>> CreateAsync(SaveCenterRequest request);
        Task<Result<CenterDto>> UpdateAsync(int centerId, SaveCenterRequest request);
        Task<Result> DeleteAsync(int centerId);
    }

    // Groups of students and their weekly lesson times.
    public interface IGroupService
    {
        Task<List<GroupDto>> ListAsync(bool includeInactive);
        Task<Result<GroupDto>> GetAsync(int groupId);
        Task<Result<GroupDto>> CreateAsync(SaveGroupRequest request);
        Task<Result<GroupDto>> UpdateAsync(int groupId, SaveGroupRequest request);
        Task<Result> SetActiveAsync(int groupId, bool isActive);
        Task<Result> DeleteAsync(int groupId);
    }

    public interface IStudentService
    {
        Task<PaginatedResult<StudentDto>> ListAsync(StudentQuery query);
        Task<Result<StudentDto>> GetAsync(int studentId);
        Task<Result<StudentDto>> CreateAsync(CreateStudentRequest request);
        Task<Result<StudentDto>> UpdateAsync(int studentId, UpdateStudentRequest request);

        // Suspends or restores the student at this teacher only.
        Task<Result> SetActiveAsync(int studentId, bool isActive);

        // Lets the student log in from a new device. Allowed at most twice per term.
        Task<Result<DeviceResetDto>> ResetDeviceAsync(int studentId);
    }

    // Teacher only: the teacher's assistants.
    public interface IAssistantService
    {
        Task<List<AssistantDto>> ListAsync();
        Task<Result<CreateAssistantResponse>> CreateAsync(CreateAssistantRequest request);
        Task<Result> SetActiveAsync(int assistantUserId, bool isActive);
    }

    // Teacher only: what the assistants did.
    public interface IAuditLogService
    {
        Task<PaginatedResult<AuditLogDto>> ListAsync(AuditLogQuery query);
    }
}
