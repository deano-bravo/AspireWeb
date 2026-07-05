using System.Security.Claims;

namespace AspireWeb.ServiceDefaults.Tenancy;

/// <summary>
/// Reads the shared tenant claims off a principal. Both the cookie principal (Web) and the
/// JWT principal (API) carry the same claim shapes, so one reader serves both.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The tenant id from the <see cref="TenantClaimTypes.TenantId"/> claim, or <c>null</c> when
    /// it is absent or unparseable. The tenant is the security boundary — callers must treat a
    /// null as "no tenant" and fail closed.
    /// </summary>
    public static Guid? GetTenantId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(TenantClaimTypes.TenantId), out var tenantId)
            ? tenantId
            : null;

    /// <summary>The tenant role from the <see cref="TenantClaimTypes.TenantRole"/> claim, or <c>null</c> when absent.</summary>
    public static string? GetTenantRole(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(TenantClaimTypes.TenantRole);

    /// <summary>The tenant name from the <see cref="TenantClaimTypes.TenantName"/> claim, or <c>null</c> when absent.</summary>
    public static string? GetTenantName(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(TenantClaimTypes.TenantName);

    /// <summary>The display name from the <see cref="TenantClaimTypes.DisplayName"/> claim, or <c>null</c> when absent.</summary>
    public static string? GetDisplayName(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(TenantClaimTypes.DisplayName);
}
