namespace AspireWeb.Contracts;

/// <summary>
/// Request body for creating a to-do item; the server trims and validates the title
/// (required, at most <c>TodoItem.TitleMaxLength</c> characters).
/// </summary>
public sealed record CreateTodoRequest(string Title);
