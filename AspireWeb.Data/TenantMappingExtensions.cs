using AspireWeb.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AspireWeb.Data;

/// <summary>
/// Single definition of the <see cref="Tenant"/> table mapping, shared by the two contexts so the
/// schema-owning side (<see cref="AppIdentityDbContext"/>) and the FK-only side
/// (<see cref="TenantDbContext"/>) cannot drift apart.
/// </summary>
internal static class TenantMappingExtensions
{
    /// <summary>Applies the shared <see cref="Tenant"/> table mapping to <paramref name="tenant"/>.</summary>
    /// <param name="tenant">The entity type builder for the <see cref="Tenant"/> entity.</param>
    /// <param name="ownsSchema">
    /// <see langword="true"/> for the context that owns the migrations (column lengths + unique
    /// index); <see langword="false"/> for the context that maps the table read-only
    /// (<c>ExcludeFromMigrations</c>).
    /// </param>
    public static void ConfigureTenant(this EntityTypeBuilder<Tenant> tenant, bool ownsSchema)
    {
        if (!ownsSchema)
        {
            // Owned by AppIdentityDbContext's migrations; mapped here only for queries + FK integrity.
            tenant.ToTable(Tenant.TableName, table => table.ExcludeFromMigrations());
            return;
        }

        tenant.ToTable(Tenant.TableName);
        tenant.Property(t => t.Identifier).HasMaxLength(Tenant.IdentifierMaxLength);
        tenant.Property(t => t.Name).HasMaxLength(Tenant.NameMaxLength);
        tenant.HasIndex(t => t.Identifier).IsUnique();
    }
}
