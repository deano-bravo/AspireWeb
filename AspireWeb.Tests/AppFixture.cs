using System.Security.Claims;
using Aspire.Hosting;
using AspireWeb.ServiceDefaults.Tenancy;
using Microsoft.Extensions.Logging;

namespace AspireWeb.Tests;

/// <summary>
/// Boots the full AppHost (Postgres container + migrations + services) once for the
/// <see cref="AppHostCollectionDefinition"/> collection. Requires a running Docker engine;
/// startup is bounded by <see cref="StartupTimeout"/> to cover a cold Postgres image pull.
/// Registration/claims helpers live in <see cref="RegistrationFlow"/>; token/request helpers in
/// <see cref="TestTokens"/>.
/// </summary>
public sealed class AppFixture : IAsyncLifetime
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(3);

    private DistributedApplication? _app;

    public DistributedApplication App =>
        _app ?? throw new InvalidOperationException("The AppHost has not started yet.");

    public async ValueTask InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AspireWeb_AppHost>();
        appHost.Configuration["Parameters:jwt-signing-key"] = TestTokens.JwtSigningKey;
        appHost.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Information);
            logging.AddFilter("Aspire.", LogLevel.Warning);
        });
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder => clientBuilder.AddStandardResilienceHandler());

        _app = await appHost.BuildAsync().WaitAsync(StartupTimeout);
        await _app.StartAsync().WaitAsync(StartupTimeout);
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("apiservice").WaitAsync(StartupTimeout);
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("webfrontend").WaitAsync(StartupTimeout);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    /// <summary>A web-front-end client with a cookie jar (CreateHttpClient has none).</summary>
    public HttpClient CreateWebClientWithCookies()
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true };
        return new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = App.GetEndpoint("webfrontend"),
        };
    }

    /// <summary>Registers a fresh organisation + owner and returns their ids from the claims.</summary>
    public async Task<(Guid TenantId, string UserId)> RegisterTenantAsync(
        string prefix, CancellationToken cancellationToken)
    {
        using var client = CreateWebClientWithCookies();
        var claims = await RegistrationFlow.RegisterAsync(
            client, RegistrationFlow.UniqueOrganization(prefix), $"{prefix}-{Guid.NewGuid():N}@example.com",
            RegistrationFlow.DefaultPassword, cancellationToken);

        var tenantId = Guid.Parse(RegistrationFlow.GetClaim(claims, TenantClaimTypes.TenantId)!);
        string userId = RegistrationFlow.GetClaim(claims, ClaimTypes.NameIdentifier)!;
        return (tenantId, userId);
    }

    /// <summary>Registers a fresh owner tenant and returns an API client plus the owner's bearer token.</summary>
    public async Task<(HttpClient Api, string OwnerToken)> CreateOwnerApiClientAsync(
        string prefix, CancellationToken cancellationToken)
    {
        var (tenantId, userId) = await RegisterTenantAsync(prefix, cancellationToken);
        string token = TestTokens.MintJwt(tenantId, userId, TenantRoleNames.Owner);
        return (App.CreateHttpClient("apiservice"), token);
    }
}
