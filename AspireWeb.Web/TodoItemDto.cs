namespace AspireWeb.Web;

public sealed record TodoItemDto(Guid Id, string Title, DateTimeOffset CreatedAt);
