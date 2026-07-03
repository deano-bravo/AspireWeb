using AspireWeb.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AspireWeb.ApiService.Tenancy;

/// <summary>
/// Checks that a tenant still exists and is active, cached for 60 seconds. Combined with
/// the 5-minute token lifetime, a deactivated tenant loses API access within ~6 minutes.
/// </summary>
public sealed class ActiveTenantGate(AppDbContext dbContext, IMemoryCache cache)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public async Task<bool> IsActiveAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await cache.GetOrCreateAsync($"tenant-active-{tenantId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await dbContext.Tenants.AnyAsync(
                tenant => tenant.Id == tenantId && tenant.IsActive, cancellationToken);
        });
    }
}
