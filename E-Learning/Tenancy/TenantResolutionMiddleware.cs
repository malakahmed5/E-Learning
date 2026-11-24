using ELearning.Shared.Security;
using ELearningHub.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELearning.API.Tenancy
{
    // Runs on every request, after the login token was checked.
    //
    // When the token says "this user chose teacher X", this middleware:
    //   1. Checks in the hub that the user still belongs to teacher X and nothing is turned off.
    //   2. Reads where teacher X's database is.
    //   3. Stores it, so the database used by this request is teacher X's database.
    //
    // The check is done on every request on purpose (no caching):
    // when a teacher suspends an assistant, the assistant must lose access at once.
    public class TenantResolutionMiddleware
    {
        private readonly RequestDelegate _next;

        public TenantResolutionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, TenantContext tenantContext, HubDbContext hub)
        {
            var hasTenant = int.TryParse(context.User.FindFirst(AppClaims.TenantId)?.Value, out var tenantId);
            var hasAccount = int.TryParse(context.User.FindFirst(AppClaims.AccountId)?.Value, out var accountId);

            // No teacher chosen (or not logged in): nothing to do here.
            if (!hasTenant || !hasAccount)
            {
                await _next(context);
                return;
            }

            var tenant = await hub.Memberships
                .AsNoTracking()
                .Where(m => m.AccountId == accountId
                            && m.TenantId == tenantId
                            && m.IsActive
                            && m.Account.IsActive
                            && m.Tenant.IsActive)
                .Select(m => new TenantInfo(m.Tenant.Id, m.Tenant.Slug, m.Tenant.ConnectionString))
                .FirstOrDefaultAsync(context.RequestAborted);

            if (tenant is null)
            {
                await WriteAccessRemovedAsync(context);
                return;
            }

            tenantContext.Set(tenant);
            await _next(context);
        }

        // The token is still valid, but the user was suspended or the teacher was turned off after login.
        private static async Task WriteAccessRemovedAsync(HttpContext context)
        {
            var problem = new ProblemDetails
            {
                Title = "Auth.TenantNotAvailable",
                Status = StatusCodes.Status403Forbidden,
                Detail = "You cannot open this teacher's platform.",
                Instance = context.Request.Path,
            };
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}
