using AspireWeb.ApiService.Tenancy;
using AspireWeb.Contracts;
using AspireWeb.Data;
using AspireWeb.Data.Entities;
using AspireWeb.ServiceDefaults;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace AspireWeb.ApiService.Endpoints;

public static class TodoEndpoints
{
    /// <summary>Scaffold cap on list size; replace with skip/take paging when the UI needs it.</summary>
    private const int MaxListLength = 200;

    /// <summary>
    /// The tenant-scoped sample resource. Reads are isolated by TenantDbContext's named
    /// "Tenant" query filter; writes are stamped/guarded by TenantSaveChangesInterceptor.
    /// </summary>
    public static IEndpointRouteBuilder MapTodoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var todos = endpoints.MapGroup("/todos")
            .WithTags("Todos")
            .RequireAuthorization(TenantPolicies.RequireTenant)
            .AddEndpointFilter<RequireActiveTenantFilter>();

        todos.MapGet("/", ListTodosAsync)
            .WithName("ListTodos")
            .Produces<TodoItemDto[]>();

        todos.MapPost("/", CreateTodoAsync)
            .WithName("CreateTodo")
            .Produces<TodoItemDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        // Query-filtered delete: another tenant's id simply isn't found. Global query
        // filters also apply to ExecuteDelete, so isolation holds without the interceptor.
        todos.MapDelete("/{id:guid}", DeleteTodoAsync)
            .WithName("DeleteTodo")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(TenantPolicies.RequireTenantAdmin);

        return endpoints;
    }

    private static async Task<TodoItemDto[]> ListTodosAsync(
        TenantDbContext dbContext, CancellationToken cancellationToken) =>
        await dbContext.TodoItems
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .Take(MaxListLength)
            .Select(item => new TodoItemDto(item.Id, item.Title, item.CreatedAt))
            .ToArrayAsync(cancellationToken);

    private static async Task<IResult> DeleteTodoAsync(
        Guid id, TenantDbContext dbContext, CancellationToken cancellationToken)
    {
        int deleted = await dbContext.TodoItems
            .Where(item => item.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted == 0 ? Results.NotFound() : Results.NoContent();
    }

    private static async Task<IResult> CreateTodoAsync(
        CreateTodoRequest request,
        TenantDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        string title = request.Title?.Trim() ?? "";
        if (title.Length == 0)
        {
            return TitleProblem("Title is required.");
        }

        if (title.Length > TodoItem.TitleMaxLength)
        {
            return TitleProblem($"Title must be at most {TodoItem.TitleMaxLength} characters.");
        }

        var item = new TodoItem
        {
            Id = Guid.NewGuid(),
            // TenantId is stamped by TenantSaveChangesInterceptor from the token's tenant.
            Title = title,
            NormalizedTitle = TodoItem.NormalizeTitle(title),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
        };
        dbContext.TodoItems.Add(item);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (PostgresErrors.IsUniqueViolation(exception))
        {
            // RFC 7807 like every other non-2xx from this API (AddProblemDetails is registered).
            return Results.Problem(
                title: "A todo with that title already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Created($"/todos/{item.Id}", new TodoItemDto(item.Id, item.Title, item.CreatedAt));
    }

    private static IResult TitleProblem(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["title"] = [message],
        });
}
