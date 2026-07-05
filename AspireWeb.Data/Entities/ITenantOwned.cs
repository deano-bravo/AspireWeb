namespace AspireWeb.Data.Entities;

/// <summary>
/// Marks an entity as owned by a tenant. TenantDbContext applies the named "Tenant"
/// global query filter to every implementation by convention, and
/// TenantSaveChangesInterceptor stamps/validates TenantId on writes.
/// </summary>
public interface ITenantOwned
{
    Guid TenantId { get; set; }
}
