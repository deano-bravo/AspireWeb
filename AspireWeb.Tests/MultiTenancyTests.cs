using System.Net.Http.Headers;
using System.Security.Claims;
using AspireWeb.ServiceDefaults;

namespace AspireWeb.Tests;

/// <summary>
/// End-to-end multi-tenancy checks against the running AppHost: registration creates a
/// tenant + owner, the API isolates data per tenant, and missing/forged tenant context
/// is rejected.
/// </summary>
public class MultiTenancyTests(AppFixture fixture)
{
    private const string Password = "Sup3r-Secret-Pass!42";

    [Fact]
    public async Task RegisterCreatesTenantAndSignsInWithTenantClaims()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = fixture.CreateWebClientWithCookies();
        string organization = UniqueOrganization("acme");

        var claims = await AppFixture.RegisterAsync(
            client, organization, $"owner-{Guid.NewGuid():N}@example.com", Password, cancellationToken);

        Assert.True(Guid.TryParse(GetClaim(claims, TenantClaimTypes.TenantId), out _));
        Assert.Equal(TenantRoleNames.Owner, GetClaim(claims, TenantClaimTypes.TenantRole));
        Assert.Equal(organization, GetClaim(claims, TenantClaimTypes.TenantName));
    }

    [Fact]
    public async Task ApiIsolatesTodosBetweenTenantsAndIgnoresSpoofedHeaders()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (tenantA, userA) = await RegisterTenantAsync("alpha", cancellationToken);
        var (tenantB, userB) = await RegisterTenantAsync("beta", cancellationToken);
        using var api = fixture.App.CreateHttpClient("apiservice");

        string secretTitle = $"alpha-secret-{Guid.NewGuid():N}";
        string tokenA = AppFixture.MintJwt(tenantA, userA, TenantRoleNames.Owner);
        string tokenB = AppFixture.MintJwt(tenantB, userB, TenantRoleNames.Owner);

        using (var create = ApiRequest(HttpMethod.Post, "/todos", tokenA))
        {
            create.Content = JsonContent.Create(new { title = secretTitle });
            using var created = await api.SendAsync(create, cancellationToken);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        Assert.Contains(await GetTodoTitlesAsync(api, tokenA, cancellationToken), title => title == secretTitle);
        Assert.DoesNotContain(await GetTodoTitlesAsync(api, tokenB, cancellationToken), title => title == secretTitle);

        // A spoofed tenant header alongside B's token must change nothing: the tenant
        // comes from the validated token only.
        using var spoofed = ApiRequest(HttpMethod.Get, "/todos", tokenB);
        spoofed.Headers.Add("X-Tenant-Id", tenantA.ToString());
        using var spoofedResponse = await api.SendAsync(spoofed, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, spoofedResponse.StatusCode);
        string[] titles = await ReadTitlesAsync(spoofedResponse, cancellationToken);
        Assert.DoesNotContain(titles, title => title == secretTitle);
    }

    [Fact]
    public async Task ApiRejectsMissingOrForgedTenantContext()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (tenantId, userId) = await RegisterTenantAsync("gamma", cancellationToken);
        using var api = fixture.App.CreateHttpClient("apiservice");

        // No token at all -> 401.
        using var anonymous = await api.GetAsync("/todos", cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        // Token signed with the wrong key -> 401.
        string forged = AppFixture.MintJwt(
            tenantId, userId, TenantRoleNames.Owner,
            signingKey: Convert.ToBase64String(Enumerable.Repeat((byte)0xFF, 32).ToArray()));
        using var forgedRequest = ApiRequest(HttpMethod.Get, "/todos", forged);
        using var forgedResponse = await api.SendAsync(forgedRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, forgedResponse.StatusCode);

        // Valid key but no tenant claim -> 403 (RequireTenant policy).
        string noTenant = AppFixture.MintJwt(tenantId: null, userId, TenantRoleNames.Owner);
        using var noTenantRequest = ApiRequest(HttpMethod.Get, "/todos", noTenant);
        using var noTenantResponse = await api.SendAsync(noTenantRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, noTenantResponse.StatusCode);

        // Member role may not delete -> 403 (RequireTenantAdmin policy).
        string member = AppFixture.MintJwt(tenantId, userId, TenantRoleNames.Member);
        using var deleteRequest = ApiRequest(HttpMethod.Delete, $"/todos/{Guid.NewGuid()}", member);
        using var deleteResponse = await api.SendAsync(deleteRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
    }

    private async Task<(Guid TenantId, string UserId)> RegisterTenantAsync(
        string prefix, CancellationToken cancellationToken)
    {
        using var client = fixture.CreateWebClientWithCookies();
        var claims = await AppFixture.RegisterAsync(
            client, UniqueOrganization(prefix), $"{prefix}-{Guid.NewGuid():N}@example.com", Password, cancellationToken);

        var tenantId = Guid.Parse(GetClaim(claims, TenantClaimTypes.TenantId)!);
        string userId = GetClaim(claims, ClaimTypes.NameIdentifier)!;
        return (tenantId, userId);
    }

    private static async Task<string[]> GetTodoTitlesAsync(
        HttpClient api, string token, CancellationToken cancellationToken)
    {
        using var request = ApiRequest(HttpMethod.Get, "/todos", token);
        using var response = await api.SendAsync(request, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadTitlesAsync(response, cancellationToken);
    }

    private static async Task<string[]> ReadTitlesAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var items = await response.Content.ReadFromJsonAsync<List<TodoItemResponse>>(cancellationToken);
        return items?.Select(item => item.Title).ToArray() ?? [];
    }

    private static HttpRequestMessage ApiRequest(HttpMethod method, string uri, string token)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static string? GetClaim(IReadOnlyList<TestClaim> claims, string type) =>
        claims.FirstOrDefault(claim => claim.Type == type)?.Value;

    private static string UniqueOrganization(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    private sealed record TodoItemResponse(Guid Id, string Title, DateTimeOffset CreatedAt);
}
