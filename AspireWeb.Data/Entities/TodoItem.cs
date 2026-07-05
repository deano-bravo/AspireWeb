namespace AspireWeb.Data.Entities;

/// <summary>A tenant-owned to-do item; <see cref="TenantId"/> is stamped on insert by the save interceptor.</summary>
public sealed class TodoItem : ITenantOwned
{
    public const int TitleMaxLength = 256;

    /// <summary>
    /// Canonical normalization for <see cref="NormalizedTitle"/>. Every write path must
    /// use this — the per-tenant unique index compares normalized values, so an
    /// inconsistent normalization would silently defeat uniqueness.
    /// </summary>
    public static string NormalizeTitle(string title) => title.Trim().ToUpperInvariant();

    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public required string Title { get; set; }

    /// <summary>Uppercased/trimmed Title; unique per tenant.</summary>
    public required string NormalizedTitle { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? CreatedByUserId { get; set; }
}
