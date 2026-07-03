using System.Net;
using System.Net.Http.Headers;
using AspireWeb.Web.Identity;

namespace AspireWeb.Web;

/// <summary>
/// Typed client for the tenant-scoped todos API. Each request carries a freshly minted
/// bearer token; the API resolves the tenant from that token alone.
/// </summary>
public class TodoApiClient(HttpClient httpClient, TenantTokenService tokenService)
{
    public async Task<TodoItemDto[]> GetTodosAsync(CancellationToken cancellationToken = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Get, "/todos");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TodoItemDto[]>(cancellationToken) ?? [];
    }

    /// <summary>Creates an item; returns null when the title already exists for this tenant.</summary>
    public async Task<TodoItemDto?> CreateTodoAsync(string title, CancellationToken cancellationToken = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Post, "/todos");
        request.Content = JsonContent.Create(new { title });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TodoItemDto>(cancellationToken);
    }

    /// <summary>Deletes an item; false when the caller lacks the admin role or the item does not exist.</summary>
    public async Task<bool> DeleteTodoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Delete, $"/todos/{id}");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private async Task<HttpRequestMessage> NewRequestAsync(HttpMethod method, string uri)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokenService.GetTokenAsync());
        return request;
    }
}
