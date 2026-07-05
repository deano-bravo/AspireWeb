using AspireWeb.Contracts;

namespace AspireWeb.Tests;

/// <summary>Shared helpers for the todos API so the integration tests post and list the same way.</summary>
internal static class TodoApiHelpers
{
    public static async Task<HttpResponseMessage> PostTodoAsync(
        this HttpClient api, string token, string title, CancellationToken cancellationToken)
    {
        using var request = TestTokens.ApiRequest(HttpMethod.Post, "/todos", token);
        request.Content = JsonContent.Create(new { title });
        return await api.SendAsync(request, cancellationToken);
    }

    public static async Task<string[]> GetTodoTitlesAsync(
        this HttpClient api, string token, CancellationToken cancellationToken)
    {
        using var request = TestTokens.ApiRequest(HttpMethod.Get, "/todos", token);
        using var response = await api.SendAsync(request, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadTitlesAsync(cancellationToken);
    }

    public static async Task<string[]> ReadTitlesAsync(
        this HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var items = await response.Content.ReadFromJsonAsync<List<TodoItemDto>>(cancellationToken);
        return items?.Select(item => item.Title).ToArray() ?? [];
    }
}
