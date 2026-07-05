using System.Security.Claims;
using AspireWeb.Data.Tenancy;
using AspireWeb.ServiceDefaults;

namespace AspireWeb.ApiService.Tenancy;

/// <summary>
/// Resolves the ambient tenant from the validated JWT principal. The tenant id is the
/// security boundary and must only ever come from a validated token — never from a header.
/// </summary>
public sealed class ClaimsTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    public Guid? TenantId =>
        Guid.TryParse(
            httpContextAccessor.HttpContext?.User.FindFirstValue(TenantClaimTypes.TenantId),
            out var tenantId)
            ? tenantId
            : null;
}
