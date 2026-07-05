using AspireWeb.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AspireWeb.ApiService.Tenancy;

/// <summary>
/// Checks that a tenant still exists and is active, cached per instance. Combined with the
/// 5-minute token lifetime, a deactivated tenant loses API access within ~6 minutes; an
/// inactive verdict is cached only briefly so a reactivated tenant is unblocked in seconds.
/// (IMemoryCache is per-pod — swap to a distributed/hybrid cache when replicas matter.)
/// </summary>
internal sealed class ActiveTenantGate(TenantDbContext dbContext, IMemoryCache cache)
{
    private static readonly TimeSpan ActiveTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan InactiveTtl = TimeSpan.FromSeconds(5);

    public async Task<bool> IsActiveAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await cache.GetOrCreateAsync($"tenant-active-{tenantId}", async entry =>
        {
            bool isActive = await dbContext.Tenants.AnyAsync(
                tenant => tenant.Id == tenantId && tenant.IsActive, cancellationToken);
            entry.AbsoluteExpirationRelativeToNow = isActive ? ActiveTtl : InactiveTtl;
            return isActive;
        });
    }
}
