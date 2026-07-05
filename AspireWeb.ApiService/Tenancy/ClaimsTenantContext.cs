using AspireWeb.Data.Tenancy;
using AspireWeb.ServiceDefaults.Tenancy;

namespace AspireWeb.ApiService.Tenancy;

/// <summary>
/// Resolves the ambient tenant from the validated JWT principal. The tenant id is the
/// security boundary and must only ever come from a validated token — never from a header.
/// </summary>
internal sealed class ClaimsTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    public Guid? TenantId => httpContextAccessor.HttpContext?.User.GetTenantId();
}
