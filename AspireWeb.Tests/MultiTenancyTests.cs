using AspireWeb.ServiceDefaults.Tenancy;

namespace AspireWeb.Tests;

/// <summary>
/// End-to-end multi-tenancy checks against the running AppHost: registration creates a
/// tenant + owner, the API isolates data per tenant, and missing/forged tenant context
/// is rejected.
/// </summary>
[Collection(AppHostCollectionDefinition.Name)]
[Trait(TestCategories.TraitName, TestCategories.Integration)]
public class MultiTenancyTests(AppFixture fixture)
{
    [Fact]
    public async Task RegisterCreatesTenantAndSignsInWithTenantClaims()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = fixture.CreateWebClientWithCookies();
        string organization = RegistrationFlow.UniqueOrganization("acme");

        var claims = await RegistrationFlow.RegisterAsync(
            client, organization, $"owner-{Guid.NewGuid():N}@example.com", RegistrationFlow.DefaultPassword, cancellationToken);

        Assert.True(Guid.TryParse(RegistrationFlow.GetClaim(claims, TenantClaimTypes.TenantId), out _));
        Assert.Equal(TenantRoleNames.Owner, RegistrationFlow.GetClaim(claims, TenantClaimTypes.TenantRole));
        Assert.Equal(organization, RegistrationFlow.GetClaim(claims, TenantClaimTypes.TenantName));
    }

    [Fact]
    public async Task ApiIsolatesTodosBetweenTenantsAndIgnoresSpoofedHeaders()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (tenantA, userA) = await fixture.RegisterTenantAsync("alpha", cancellationToken);
        var (tenantB, userB) = await fixture.RegisterTenantAsync("beta", cancellationToken);
        using var api = fixture.App.CreateHttpClient("apiservice");

        string secretTitle = $"alpha-secret-{Guid.NewGuid():N}";
        string tokenA = TestTokens.MintJwt(tenantA, userA, TenantRoleNames.Owner);
        string tokenB = TestTokens.MintJwt(tenantB, userB, TenantRoleNames.Owner);

        using (var create = TestTokens.ApiRequest(HttpMethod.Post, "/todos", tokenA))
        {
            create.Content = JsonContent.Create(new { title = secretTitle });
            using var created = await api.SendAsync(create, cancellationToken);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        Assert.Contains(await api.GetTodoTitlesAsync(tokenA, cancellationToken), title => title == secretTitle);
        Assert.DoesNotContain(await api.GetTodoTitlesAsync(tokenB, cancellationToken), title => title == secretTitle);

        // A spoofed tenant header alongside B's token must change nothing: the tenant
        // comes from the validated token only.
        using var spoofed = TestTokens.ApiRequest(HttpMethod.Get, "/todos", tokenB);
        spoofed.Headers.Add("X-Tenant-Id", tenantA.ToString());
        using var spoofedResponse = await api.SendAsync(spoofed, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, spoofedResponse.StatusCode);
        string[] titles = await spoofedResponse.ReadTitlesAsync(cancellationToken);
        Assert.DoesNotContain(titles, title => title == secretTitle);
    }

    [Fact]
    public async Task ApiRejectsMissingOrForgedTenantContext()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (tenantId, userId) = await fixture.RegisterTenantAsync("gamma", cancellationToken);
        using var api = fixture.App.CreateHttpClient("apiservice");

        // No token at all -> 401.
        using var anonymous = await api.GetAsync("/todos", cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        // Token signed with the wrong key -> 401.
        string forged = TestTokens.MintJwt(
            tenantId, userId, TenantRoleNames.Owner,
            signingKey: Convert.ToBase64String(Enumerable.Repeat((byte)0xFF, 32).ToArray()));
        using var forgedRequest = TestTokens.ApiRequest(HttpMethod.Get, "/todos", forged);
        using var forgedResponse = await api.SendAsync(forgedRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, forgedResponse.StatusCode);

        // Valid key but no tenant claim -> 403 (RequireTenant policy).
        string noTenant = TestTokens.MintJwt(tenantId: null, userId, TenantRoleNames.Owner);
        using var noTenantRequest = TestTokens.ApiRequest(HttpMethod.Get, "/todos", noTenant);
        using var noTenantResponse = await api.SendAsync(noTenantRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, noTenantResponse.StatusCode);

        // Member role may not delete -> 403 (RequireTenantAdmin policy).
        string member = TestTokens.MintJwt(tenantId, userId, TenantRoleNames.Member);
        using var deleteRequest = TestTokens.ApiRequest(HttpMethod.Delete, $"/todos/{Guid.NewGuid()}", member);
        using var deleteResponse = await api.SendAsync(deleteRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
    }
}
