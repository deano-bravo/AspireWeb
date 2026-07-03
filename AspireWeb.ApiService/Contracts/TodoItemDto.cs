namespace AspireWeb.ApiService.Contracts;

/// <summary>TenantId is deliberately absent: the tenant comes from the token, never the payload.</summary>
public sealed record TodoItemDto(Guid Id, string Title, DateTimeOffset CreatedAt);
