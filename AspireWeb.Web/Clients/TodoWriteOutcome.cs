namespace AspireWeb.Web.Clients;

/// <summary>
/// Outcome of a write against the todos API, so the UI can report honestly — a permission
/// problem, a duplicate, a missing row, and a server fault are different messages.
/// </summary>
public enum TodoWriteOutcome
{
    Success,
    DuplicateTitle,
    Forbidden,
    NotFound,
    Failed,
}
