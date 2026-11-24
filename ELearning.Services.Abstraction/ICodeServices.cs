using ELearning.Shared.CommonResponses;
using ELearning.Shared.DTOs;
using ELearning.Shared.DTOs.Codes;

namespace ELearning.Services.Abstraction
{
    // Platform owner only: generates and manages subscription codes. Works only with the hub database.
    public interface ICodeAdminService
    {
        Task<Result<GeneratedBatchDto>> GenerateAsync(GenerateCodesRequest request);
        Task<PaginatedResult<CodeDto>> ListAsync(CodeQuery query);
        Task<List<CodeStatsDto>> GetStatsAsync();

        // Cancels a code that was not used yet.
        Task<Result> RevokeAsync(int codeId);

        // All codes of one batch, with dashes, for exporting to a file.
        Task<Result<List<string>>> ExportBatchAsync(int batchId);

        Task<PaginatedResult<PlatformAuditLogDto>> ListAuditLogAsync(PlatformAuditLogQuery query);
    }

    // A logged-in student at the chosen teacher: enter a code and see the subscription.
    public interface ISubscriptionService
    {
        Task<Result<RedeemResultDto>> RedeemAsync(RedeemCodeRequest request);
        Task<Result<SubscriptionStatusDto>> GetMyStatusAsync();
    }

    // A new student who has no password yet: opens the account with a code.
    public interface IActivationService
    {
        Task<Result<RedeemResultDto>> ActivateAsync(ActivateAccountRequest request);
    }
}
