using ELearning.Shared.CommonResponses;
using ELearning.Shared.DTOs.Admin;

namespace ELearning.Services.Abstraction
{
    // Platform team only: register and manage teachers.
    public interface ITenantAdminService
    {
        // Registers a teacher and creates the teacher's own database.
        Task<Result<CreateTenantResponse>> CreateAsync(CreateTenantRequest request);

        Task<List<TenantDto>> ListAsync();

        // Turns a teacher's platform on or off.
        Task<Result> SetStatusAsync(int tenantId, bool isActive);

        // Gives the teacher a new one-time code to set a new password.
        Task<Result<InviteDto>> ResetTeacherInviteAsync(int tenantId);
    }

    // Platform team only: the dates of each school term.
    public interface IAcademicTermService
    {
        Task<List<AcademicTermDto>> ListAsync();
        Task<Result<AcademicTermDto>> CreateAsync(CreateAcademicTermRequest request);
    }
}
