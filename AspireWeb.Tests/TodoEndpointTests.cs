using System.Text.Json;
using AspireWeb.Contracts;
using AspireWeb.Data.Entities;

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
        var (api, token) = await fixture.CreateOwnerApiClientAsync("empty", cancellationToken);
        using var client = api;

        using var response = await client.PostTodoAsync(token, "   ", cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ReadProblemAsync(response, "errors", cancellationToken);
        Assert.True(errors.TryGetProperty("title", out _));
    }

    [Fact]
    public async Task CreateTodoWithOverlongTitleReturnsValidationProblem()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (api, token) = await fixture.CreateOwnerApiClientAsync("long", cancellationToken);
        using var client = api;

        using var response = await client.PostTodoAsync(
            token, new string('x', TodoItem.TitleMaxLength + 1), cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ReadProblemAsync(response, "errors", cancellationToken);
        Assert.True(errors.TryGetProperty("title", out _));
    }

    [Fact]
    public async Task CreateTodoWithDuplicateTitleReturnsConflictProblem()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (api, token) = await fixture.CreateOwnerApiClientAsync("dup", cancellationToken);
        using var client = api;
        string title = $"Report {Guid.NewGuid():N}";

        using var created = await client.PostTodoAsync(token, title, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // Same title after normalization (trim + uppercase) — exercises the unique index.
        using var duplicate = await client.PostTodoAsync(token, $"  {title.ToUpperInvariant()} ", cancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var problemTitle = await ReadProblemAsync(duplicate, "title", cancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(problemTitle.GetString()));
    }

    [Fact]
    public async Task OwnerCanDeleteTodo()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (api, token) = await fixture.CreateOwnerApiClientAsync("delete", cancellationToken);
        using var client = api;
        string title = $"disposable-{Guid.NewGuid():N}";

        using var created = await client.PostTodoAsync(token, title, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var item = await created.Content.ReadFromJsonAsync<TodoItemDto>(cancellationToken);
        Assert.NotNull(item);

        using var deleteRequest = TestTokens.ApiRequest(HttpMethod.Delete, $"/todos/{item.Id}", token);
        using var deleted = await client.SendAsync(deleteRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        Assert.DoesNotContain(await client.GetTodoTitlesAsync(token, cancellationToken), remaining => remaining == title);
    }

    [Fact]
    public async Task DeleteMissingTodoReturnsNotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (api, token) = await fixture.CreateOwnerApiClientAsync("missing", cancellationToken);
        using var client = api;

        using var request = TestTokens.ApiRequest(HttpMethod.Delete, $"/todos/{Guid.NewGuid()}", token);
        using var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
