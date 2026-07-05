namespace AspireWeb.ServiceDefaults.Tenancy;

/// <summary>Authorization policy names shared by the web front end and the API service.</summary>
public static class TenantPolicies
{
    public const string RequireTenant = nameof(RequireTenant);
    public const string RequireTenantAdmin = nameof(RequireTenantAdmin);
    // Registered for completeness but not yet applied to any endpoint; reserved for owner-only
    // verbs (e.g. tenant deletion, billing) when they land — intentionally not dead code.
    public const string RequireTenantOwner = nameof(RequireTenantOwner);
}
