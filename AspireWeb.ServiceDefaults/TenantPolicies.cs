namespace AspireWeb.ServiceDefaults;

/// <summary>Authorization policy names shared by the web front end and the API service.</summary>
public static class TenantPolicies
{
    public const string RequireTenant = nameof(RequireTenant);
    public const string RequireTenantAdmin = nameof(RequireTenantAdmin);
    public const string RequireTenantOwner = nameof(RequireTenantOwner);
}
