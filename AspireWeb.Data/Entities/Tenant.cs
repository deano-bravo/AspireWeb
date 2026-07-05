namespace AspireWeb.Data.Entities;

/// <summary>
/// A tenant (organisation). Shape kept compatible with Finbuckle's ITenantInfo
/// (Id / Identifier / Name) so the hand-rolled tenancy layer can be swapped for
/// Finbuckle.MultiTenant once it ships an EF-11-compatible release.
/// </summary>
public sealed class Tenant
{
    /// <summary>
    /// Single source of truth for the table name: ApplicationDbContext owns the table,
    /// AppDbContext maps it FK-only (ExcludeFromMigrations) — both must agree.
    /// </summary>
    public const string TableName = "Tenants";

    public Guid Id { get; set; }

    /// <summary>Unique, normalized slug (e.g. "acme").</summary>
    public required string Identifier { get; set; }

    /// <summary>Display name as entered at registration.</summary>
    public required string Name { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
}
