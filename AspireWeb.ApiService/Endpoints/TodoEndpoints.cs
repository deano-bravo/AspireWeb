using AspireWeb.ApiService.Contracts;
using AspireWeb.ApiService.Tenancy;
using AspireWeb.Data;
using AspireWeb.Data.Entities;
using AspireWeb.ServiceDefaults;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Npgsql;

namespace AspireWeb.ApiService.Endpoints;

public static class TodoEndpoints
{
    /// <summary>
    /// The tenant-scoped sample resource. Reads are isolated by AppDbContext's named
    /// "Tenant" query filter; writes are stamped/guarded by TenantSaveChangesInterceptor.
    /// </summary>
    public static IEndpointRouteBuilder MapTodoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var todos = endpoints.MapGroup("/todos")
            .RequireAuthorization(TenantPolicies.RequireTenant)
            .AddEndpointFilter<RequireActiveTenantFilter>();

        todos.MapGet("/", async (AppDbContext dbContext, CancellationToken cancellationToken) =>
            await dbContext.TodoItems
                .OrderBy(item => item.CreatedAt)
                .Select(item => new TodoItemDto(item.Id, item.Title, item.CreatedAt))
                .ToArrayAsync(cancellationToken));

        todos.MapPost("/", CreateTodoAsync);

        // Query-filtered delete: another tenant's id simply isn't found. Global query
        // filters also apply to ExecuteDelete, so isolation holds without the interceptor.
        todos.MapDelete("/{id:guid}", async (Guid id, AppDbContext dbContext, CancellationToken cancellationToken) =>
            {
                int deleted = await dbContext.TodoItems
                    .Where(item => item.Id == id)
                    .ExecuteDeleteAsync(cancellationToken);
                return deleted == 0 ? Results.NotFound() : Results.NoContent();
            })
            .RequireAuthorization(TenantPolicies.RequireTenantAdmin);

        return endpoints;
    }

    private static async Task<IResult> CreateTodoAsync(
        CreateTodoRequest request,
        AppDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        string title = request.Title?.Trim() ?? "";
        if (title.Length == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["title"] = ["Title is required."],
            });
        }

        var item = new TodoItem
        {
            Id = Guid.NewGuid(),
            // TenantId is stamped by TenantSaveChangesInterceptor from the token's tenant.
            Title = title,
            NormalizedTitle = title.ToUpperInvariant(),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
        };
        dbContext.TodoItems.Add(item);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Results.Conflict(new { error = "A todo with that title already exists." });
        }

        return Results.Created($"/todos/{item.Id}", new TodoItemDto(item.Id, item.Title, item.CreatedAt));
    }
}
