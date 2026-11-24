using ELearning.Core.Contracts;

namespace ELearning.API.Tenancy
{
    // The teacher of the current request. Filled once per request by TenantResolutionMiddleware.
    public class TenantContext : ITenantProvider
    {
        public int? TenantId { get; private set; }
        public string? Slug { get; private set; }
        public string? ConnectionString { get; private set; }

        public void Set(TenantInfo tenant)
        {
            TenantId = tenant.Id;
            Slug = tenant.Slug;
            ConnectionString = tenant.ConnectionString;
        }
    }

    public record TenantInfo(int Id, string Slug, string ConnectionString);
}
