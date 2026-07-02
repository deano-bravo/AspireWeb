using System.Linq.Expressions;
using AspireWeb.Data.Entities;
using AspireWeb.Data.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace AspireWeb.Data;

/// <summary>
/// Tenant-scoped business data, used by the API service and the migration service.
/// Every ITenantOwned entity gets the named "Tenant" global query filter by convention;
/// never call the bare IgnoreQueryFilters() — use IgnoreQueryFilters([TenantFilterName])
/// only where crossing tenants is explicitly justified.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
    : DbContext(options)
{
    public const string MigrationsHistoryTableName = "__ef_migrations_app";
    public const string TenantFilterName = "Tenant";

    /// <summary>
    /// Referenced by the tenant query filter; EF re-binds it per context instance.
    /// Guid.Empty matches no rows: no ambient tenant means queries fail closed.
    /// </summary>
    public Guid CurrentTenantId { get; } = tenantContext.TenantId ?? Guid.Empty;

    public DbSet<TodoItem> TodoItems => Set<TodoItem>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Owned by ApplicationDbContext's migrations; mapped here only for queries + FK integrity.
        modelBuilder.Entity<Tenant>().ToTable("Tenants", t => t.ExcludeFromMigrations());

        modelBuilder.Entity<TodoItem>(item =>
        {
            item.Property(i => i.Title).HasMaxLength(256);
            item.Property(i => i.NormalizedTitle).HasMaxLength(256);
            item.HasOne<Tenant>().WithMany().HasForeignKey(i => i.TenantId);
            item.HasIndex(i => new { i.TenantId, i.NormalizedTitle }).IsUnique();
        });

        ApplyTenantFilter(modelBuilder);
    }

    /// <summary>Row-level tenancy: one named filter applied to every ITenantOwned entity.</summary>
    private void ApplyTenantFilter(ModelBuilder modelBuilder)
    {
        var tenantOwnedTypes = modelBuilder.Model.GetEntityTypes()
            .Select(entityType => entityType.ClrType)
            .Where(clrType => typeof(ITenantOwned).IsAssignableFrom(clrType));

        foreach (var clrType in tenantOwnedTypes)
        {
            var entity = Expression.Parameter(clrType, "entity");
            var filter = Expression.Lambda(
                Expression.Equal(
                    Expression.Property(entity, nameof(ITenantOwned.TenantId)),
                    Expression.Property(Expression.Constant(this), nameof(CurrentTenantId))),
                entity);

            modelBuilder.Entity(clrType).HasQueryFilter(TenantFilterName, filter);
            modelBuilder.Entity(clrType).HasIndex(nameof(ITenantOwned.TenantId));
        }
    }
}
