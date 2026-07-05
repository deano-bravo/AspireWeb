using AspireWeb.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AspireWeb.Data.Tenancy;

/// <summary>
/// Write-side tenancy guard: stamps TenantId on new tenant-owned entities and blocks
/// any write that targets another tenant (the query filter only protects reads).
/// </summary>
public sealed class TenantSaveChangesInterceptor(ITenantContext tenantContext) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        EnforceTenant(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        EnforceTenant(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void EnforceTenant(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var currentTenant = tenantContext.TenantId
                ?? throw new InvalidOperationException(
                    $"A tenant-owned entity ({entry.Metadata.DisplayName()}) was written without an ambient tenant.");

            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
            {
                entry.Entity.TenantId = currentTenant;
                continue;
            }

            if (entry.Entity.TenantId != currentTenant)
            {
                throw new InvalidOperationException(
                    $"Cross-tenant write blocked for {entry.Metadata.DisplayName()}.");
            }

            if (entry.State == EntityState.Modified && entry.Property(e => e.TenantId).IsModified)
            {
                throw new InvalidOperationException("TenantId is immutable.");
            }
        }
    }
}
