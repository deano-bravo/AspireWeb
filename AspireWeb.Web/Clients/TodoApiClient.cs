using System.Net;
using System.Net.Http.Headers;
using AspireWeb.Contracts;
using AspireWeb.Web.Identity;

namespace AspireWeb.Web.Clients;

/// <summary>
/// Typed client for the tenant-scoped todos API. Each request carries a freshly minted
/// bearer token; the API resolves the tenant from that token alone.
/// </summary>
public sealed partial class TodoApiClient(
    HttpClient httpClient,
    TenantTokenService tokenService,
    ILogger<TodoApiClient> logger)
{
    /// <summary>Throws <see cref="HttpRequestException"/> on failure; the page owns the catch.</summary>
    public async Task<TodoItemDto[]> GetTodosAsync(CancellationToken cancellationToken = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Get, "/todos", cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TodoItemDto[]>(cancellationToken) ?? [];
    }

    public async Task<TodoWriteOutcome> CreateTodoAsync(string title, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = await NewRequestAsync(HttpMethod.Post, "/todos", cancellationToken);
            request.Content = JsonContent.Create(new CreateTodoRequest(title));
            using var response = await httpClient.SendAsync(request, cancellationToken);
            return ToOutcome(response, nameof(CreateTodoAsync));
        }
        catch (HttpRequestException exception)
        {
            // The standard resilience handler has already retried transient failures.
            LogTransportFailure(nameof(CreateTodoAsync), exception);
            return TodoWriteOutcome.Failed;
        }
    }

    public async Task<TodoWriteOutcome> DeleteTodoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = await NewRequestAsync(HttpMethod.Delete, $"/todos/{id}", cancellationToken);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            return ToOutcome(response, nameof(DeleteTodoAsync));
        }
        catch (HttpRequestException exception)
        {
            LogTransportFailure(nameof(DeleteTodoAsync), exception);
            return TodoWriteOutcome.Failed;
        }
    }

    private TodoWriteOutcome ToOutcome(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode)
        {
            return TodoWriteOutcome.Success;
        }

        var outcome = response.StatusCode switch
        {
            HttpStatusCode.Conflict => TodoWriteOutcome.DuplicateTitle,
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => TodoWriteOutcome.Forbidden,
            HttpStatusCode.NotFound => TodoWriteOutcome.NotFound,
            _ => TodoWriteOutcome.Failed,
        };

        if (outcome == TodoWriteOutcome.Failed)
        {
            LogUnexpectedStatus(operation, (int)response.StatusCode);
        }

        return outcome;
    }

    private async Task<HttpRequestMessage> NewRequestAsync(HttpMethod method, string uri, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", await tokenService.GetTokenAsync(cancellationToken));
        return request;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Todo API {Operation} failed with a transport error.")]
    private partial void LogTransportFailure(string operation, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Todo API {Operation} returned unexpected status {StatusCode}.")]
    private partial void LogUnexpectedStatus(string operation, int statusCode);
}
