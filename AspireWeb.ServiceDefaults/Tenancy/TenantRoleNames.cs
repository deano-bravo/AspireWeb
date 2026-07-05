namespace AspireWeb.ServiceDefaults.Tenancy;

/// <summary>
/// The tenant_role claim values. Must stay in sync with the TenantRole enum in
/// AspireWeb.Data (claims carry the enum member names as strings).
/// </summary>
public static class TenantRoleNames
{
    public const string Member = nameof(Member);
    public const string Admin = nameof(Admin);
    public const string Owner = nameof(Owner);
}
