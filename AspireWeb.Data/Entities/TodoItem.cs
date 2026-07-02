namespace AspireWeb.Data.Entities;

public sealed class TodoItem : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public required string Title { get; set; }

    /// <summary>Uppercased/trimmed Title; unique per tenant.</summary>
    public required string NormalizedTitle { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? CreatedByUserId { get; set; }
}
