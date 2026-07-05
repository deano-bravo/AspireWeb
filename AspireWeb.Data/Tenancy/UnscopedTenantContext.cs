namespace AspireWeb.Data.Tenancy;

/// <summary>
/// No ambient tenant. Used at design time and by the migration service, which
/// never read or write tenant-owned rows.
/// </summary>
public sealed class UnscopedTenantContext : ITenantContext
{
    public Guid? TenantId => null;
}
