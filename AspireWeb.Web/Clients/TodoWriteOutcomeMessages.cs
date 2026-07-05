namespace AspireWeb.Web.Clients;

/// <summary>
/// Single outcome→message mapping shared by every write to the todos API, so the UI reports a
/// permission problem, a duplicate, a missing row, and a server fault consistently.
/// </summary>
public static class TodoWriteOutcomeMessages
{
    /// <summary>The user-facing message for a non-success outcome, or <c>null</c> for success.</summary>
    public static string? Describe(this TodoWriteOutcome outcome, TodoWriteOperation operation) => (outcome, operation) switch
    {
        (TodoWriteOutcome.Success, _) => null,
        (TodoWriteOutcome.DuplicateTitle, _) => "A todo with that title already exists.",
        (TodoWriteOutcome.Forbidden, TodoWriteOperation.Add) => "You are not allowed to add todos.",
        (TodoWriteOutcome.Forbidden, TodoWriteOperation.Delete) => "Only organisation admins or owners can delete todos.",
        (TodoWriteOutcome.NotFound, _) => "That todo was already removed.",
        (TodoWriteOutcome.Failed, TodoWriteOperation.Add) => "The todo could not be added — please try again.",
        (TodoWriteOutcome.Failed, TodoWriteOperation.Delete) => "The todo could not be deleted — please try again.",
        _ => "The action could not be completed — please try again.",
    };
}
