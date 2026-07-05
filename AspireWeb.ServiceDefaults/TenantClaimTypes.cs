namespace AspireWeb.ServiceDefaults;

/// <summary>
/// The tenant claims contract shared by the web front end (cookie principal) and the
/// API service (JWT principal). Keep the two principals shape-identical so one policy
/// set serves both.
/// </summary>
public static class TenantClaimTypes
{
    public const string TenantId = "tenant_id";
    public const string TenantName = "tenant_name";
    public const string TenantRole = "tenant_role";
    public const string DisplayName = "display_name";
}
