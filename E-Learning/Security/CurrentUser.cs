using ELearning.Core.Contracts;
using ELearning.Services.Abstraction;
using ELearning.Shared.Security;
using System.Security.Claims;

namespace ELearning.API.Security
{
    // Reads "who is calling" from the login token of the current request.
    // The token is signed by us and checked before this code runs, so these values can be trusted.
    public class CurrentUser : ICurrentUser, IAuditActor
    {
        // For the activity log: who is changing data, and must it be recorded?
        int? IAuditActor.UserId => TenantUserId;

        // Business rule: everything an assistant changes is recorded for the teacher.
        bool IAuditActor.ShouldAudit => Role == AppRoles.Assistant && TenantUserId is not null;

        private readonly ClaimsPrincipal? _user;

        public CurrentUser(IHttpContextAccessor httpContextAccessor)
        {
            _user = httpContextAccessor.HttpContext?.User;
        }

        public int AccountId => ReadNumber(AppClaims.AccountId) ?? 0;

        public bool IsPlatformAdmin => _user?.FindFirst(AppClaims.IsPlatformAdmin)?.Value == "true";

        public int? TenantId => ReadNumber(AppClaims.TenantId);

        public int? TenantUserId => ReadNumber(AppClaims.TenantUserId);

        public string? Role => _user?.FindFirst(AppClaims.Role)?.Value;

        private int? ReadNumber(string claimName)
        {
            var text = _user?.FindFirst(claimName)?.Value;
            return int.TryParse(text, out var number) ? number : null;
        }
    }
}
