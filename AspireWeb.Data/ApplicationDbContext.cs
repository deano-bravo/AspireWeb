using AspireWeb.Data.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AspireWeb.Data;

/// <summary>
/// Identity + tenant registry + DataProtection keys. Used by the Web front end and
/// the migration service. Owns the Tenants table (AppDbContext maps it FK-only).
/// </summary>
public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IDataProtectionKeyContext
{
    public const string MigrationsHistoryTableName = "__ef_migrations_identity";

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Tenant>(tenant =>
        {
            tenant.ToTable(Tenant.TableName);
            tenant.Property(t => t.Identifier).HasMaxLength(64);
            tenant.Property(t => t.Name).HasMaxLength(128);
            tenant.HasIndex(t => t.Identifier).IsUnique();
        });

        builder.Entity<ApplicationUser>(user =>
        {
            user.HasOne(u => u.Tenant).WithMany().HasForeignKey(u => u.TenantId);
            user.Property(u => u.DisplayName).HasMaxLength(128);
        });
    }
}
