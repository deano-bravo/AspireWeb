namespace AspireWeb.Data.Tenancy;

/// <summary>
/// The ambient tenant for the current scope. Null means "no tenant": the query
/// filter then matches no rows and tenant-owned writes throw (fail closed).
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }
}
