using System.Text.Json;
using AspireWeb.Contracts;
using AspireWeb.Data.Entities;
using AspireWeb.ServiceDefaults;

namespace AspireWeb.Tests;

/// <summary>
/// Contract tests for the /todos endpoints beyond the happy path: RFC 7807 validation and
/// conflict responses, and the admin delete's 204/404 branches (the 403 branch is covered
/// in MultiTenancyTests).
/// </summary>
[Collection(AppHostCollectionDefinition.Name)]
[Trait(TestCategories.TraitName, TestCategories.Integration)]
public class TodoEndpointTests(AppFixture fixture)
{
    [Fact]
    public async Task CreateTodoWithEmptyTitleReturnsValidationProblem()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (api, token) = await CreateOwnerApiClientAsync("empty", cancellationToken);
        using var client = api;

        using var response = await PostTodoAsync(client, token, "   ", cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ReadProblemAsync(response, "errors", cancellationToken);
        Assert.True(errors.TryGetProperty("title", out _));
    }

    [Fact]
    public async Task CreateTodoWithOverlongTitleReturnsValidationProblem()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (api, token) = await CreateOwnerApiClientAsync("long", cancellationToken);
        using var client = api;

        using var response = await PostTodoAsync(
            client, token, new string('x', TodoItem.TitleMaxLength + 1), cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ReadProblemAsync(response, "errors", cancellationToken);
        Assert.True(errors.TryGetProperty("title", out _));
    }

    [Fact]
    public async Task CreateTodoWithDuplicateTitleReturnsConflictProblem()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (api, token) = await CreateOwnerApiClientAsync("dup", cancellationToken);
        using var client = api;
        string title = $"Report {Guid.NewGuid():N}";

        using var created = await PostTodoAsync(client, token, title, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // Same title after normalization (trim + uppercase) — exercises the unique index.
        using var duplicate = await PostTodoAsync(client, token, $"  {title.ToUpperInvariant()} ", cancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var problemTitle = await ReadProblemAsync(duplicate, "title", cancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(problemTitle.GetString()));
    }

    [Fact]
    public async Task OwnerCanDeleteTodo()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (api, token) = await CreateOwnerApiClientAsync("delete", cancellationToken);
        using var client = api;
        string title = $"disposable-{Guid.NewGuid():N}";

        using var created = await PostTodoAsync(client, token, title, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var item = await created.Content.ReadFromJsonAsync<TodoItemDto>(cancellationToken);
        Assert.NotNull(item);

        using var deleteRequest = AppFixture.ApiRequest(HttpMethod.Delete, $"/todos/{item.Id}", token);
        using var deleted = await client.SendAsync(deleteRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        using var listRequest = AppFixture.ApiRequest(HttpMethod.Get, "/todos", token);
        using var list = await client.SendAsync(listRequest, cancellationToken);
        var remaining = await list.Content.ReadFromJsonAsync<List<TodoItemDto>>(cancellationToken);
        Assert.DoesNotContain(remaining ?? [], todo => todo.Title == title);
    }

    [Fact]
    public async Task DeleteMissingTodoReturnsNotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (api, token) = await CreateOwnerApiClientAsync("missing", cancellationToken);
        using var client = api;

        using var request = AppFixture.ApiRequest(HttpMethod.Delete, $"/todos/{Guid.NewGuid()}", token);
        using var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<(HttpClient Api, string OwnerToken)> CreateOwnerApiClientAsync(
        string prefix, CancellationToken cancellationToken)
    {
        var (tenantId, userId) = await fixture.RegisterTenantAsync($"todoapi-{prefix}", cancellationToken);
        string token = AppFixture.MintJwt(tenantId, userId, TenantRoleNames.Owner);
        return (fixture.App.CreateHttpClient("apiservice"), token);
    }

    private static async Task<HttpResponseMessage> PostTodoAsync(
        HttpClient api, string token, string title, CancellationToken cancellationToken)
    {
        using var request = AppFixture.ApiRequest(HttpMethod.Post, "/todos", token);
        request.Content = JsonContent.Create(new { title });
        return await api.SendAsync(request, cancellationToken);
    }

    /// <summary>Asserts the RFC 7807 content type and returns the requested root property.</summary>
    private static async Task<JsonElement> ReadProblemAsync(
        HttpResponseMessage response, string property, CancellationToken cancellationToken)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.GetProperty(property).Clone();
    }
}
