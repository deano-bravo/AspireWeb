using System.Text.RegularExpressions;
using Aspire.Hosting;
using AspireWeb.ServiceDefaults;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

[assembly: AssemblyFixture(typeof(AspireWeb.Tests.AppFixture))]

namespace AspireWeb.Tests;

/// <summary>
/// Boots the full AppHost (Postgres container + migrations + services) once per test
/// assembly. Requires a running Docker engine; startup is bounded by
/// <see cref="StartupTimeout"/> to cover a cold Postgres image pull.
/// </summary>
public sealed partial class AppFixture : IAsyncLifetime
{
    /// <summary>Deterministic signing key so tests can mint their own API tokens.</summary>
    public const string JwtSigningKey = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";

    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(3);

    private DistributedApplication? _app;

    public DistributedApplication App =>
        _app ?? throw new InvalidOperationException("The AppHost has not started yet.");

    public async ValueTask InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AspireWeb_AppHost>();
        appHost.Configuration["Parameters:jwt-signing-key"] = JwtSigningKey;
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

    /// <summary>
    /// Drives the real Register page: fetches it, re-posts every hidden field (antiforgery
    /// token, form handler) plus the visible inputs, then proves the client is signed in by
    /// returning the principal's claims from the dev-only /debug/claims endpoint.
    /// </summary>
    public static async Task<IReadOnlyList<TestClaim>> RegisterAsync(
        HttpClient client, string organization, string email, string password, CancellationToken cancellationToken)
    {
        string page = await client.GetStringAsync("/Account/Register", cancellationToken);

        var form = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match hidden in HiddenInputRegex().Matches(page))
        {
            var name = NameAttributeRegex().Match(hidden.Value);
            if (name.Success)
            {
                var value = ValueAttributeRegex().Match(hidden.Value);
                form[name.Groups["name"].Value] = value.Success ? value.Groups["value"].Value : "";
            }
        }

        form["Input.OrganizationName"] = organization;
        form["Input.Email"] = email;
        form["Input.Password"] = password;
        form["Input.ConfirmPassword"] = password;

        string action = FormActionRegex().Match(page) is { Success: true } match && match.Groups["action"].Value.Length > 0
            ? match.Groups["action"].Value
            : "/Account/Register";

        using var content = new FormUrlEncodedContent(form);
        using var response = await client.PostAsync(action, content, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await GetClaimsAsync(client, cancellationToken);
    }

    /// <summary>Reads the signed-in principal's claims via the dev-only /debug/claims endpoint.</summary>
    public static async Task<IReadOnlyList<TestClaim>> GetClaimsAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var claims = await client.GetFromJsonAsync<List<TestClaim>>("/debug/claims", cancellationToken);
        Assert.NotNull(claims);
        return claims;
    }

    /// <summary>Mints an API token the way the web front end does (or a forged/partial one).</summary>
    public static string MintJwt(
        Guid? tenantId, string userId, string role = TenantRoleNames.Member, string? signingKey = null)
    {
        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [JwtRegisteredClaimNames.Sub] = userId,
            [TenantClaimTypes.TenantRole] = role,
        };
        if (tenantId is { } id)
        {
            claims[TenantClaimTypes.TenantId] = id.ToString();
        }

        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = ApiJwtDefaults.Issuer,
            Audience = ApiJwtDefaults.Audience,
            IssuedAt = now,
            Expires = now.Add(ApiJwtDefaults.TokenLifetime),
            Claims = claims,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Convert.FromBase64String(signingKey ?? JwtSigningKey)),
                SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    [GeneratedRegex("<input\\b[^>]*type=\"hidden\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex HiddenInputRegex();

    [GeneratedRegex("name=\"(?<name>[^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex NameAttributeRegex();

    [GeneratedRegex("value=\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex ValueAttributeRegex();

    [GeneratedRegex("<form\\b[^>]*method=\"post\"[^>]*action=\"(?<action>[^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex FormActionRegex();
}
