namespace AspireWeb.Data.Tenancy;

/// <summary>An explicitly chosen tenant, for seeding and tests.</summary>
public sealed class FixedTenantContext(Guid tenantId) : ITenantContext
{
    public Guid? TenantId { get; } = tenantId;
}
