using System.Security.Claims;
using AspireWeb.ServiceDefaults.Tenancy;
using AspireWeb.Web.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AspireWeb.Tests;

/// <summary>
/// Unit tests for the real token minter (no AppHost). Tokens are asserted by validating
/// them with the exact parameters the API enforces, so the web→api contract cannot drift
/// between this service and the API's JwtBearer configuration.
/// </summary>
public class TenantTokenServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private const string UserId = "user-1";

    [Fact]
    public async Task MintsTokenThatValidatesUnderApiParameters()
    {
        var service = CreateService(OwnerPrincipal());

        string token = await service.GetTokenAsync(TestContext.Current.CancellationToken);

        var result = await ValidateAsync(token);
        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal(UserId, GetClaim(result, JwtRegisteredClaimNames.Sub));
        Assert.Equal(TenantId.ToString(), GetClaim(result, TenantClaimTypes.TenantId));
        Assert.Equal(TenantRoleNames.Owner, GetClaim(result, TenantClaimTypes.TenantRole));
    }

    [Fact]
    public async Task TokenExpiresAfterConfiguredLifetime()
    {
        var now = DateTimeOffset.UtcNow;
        var service = CreateService(OwnerPrincipal(), new FakeTimeProvider(now));

        string token = await service.GetTokenAsync(TestContext.Current.CancellationToken);

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);
        var expected = now.Add(ApiJwtDefaults.TokenLifetime).UtcDateTime;
        // JWT timestamps are whole epoch seconds — allow the truncation.
        Assert.InRange(jwt.ValidTo, expected.AddSeconds(-2), expected.AddSeconds(2));
    }

    [Fact]
    public async Task ReturnsCachedTokenWithinLifetime()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var service = CreateService(OwnerPrincipal(), time);
        var cancellationToken = TestContext.Current.CancellationToken;

        string first = await service.GetTokenAsync(cancellationToken);
        time.Advance(TimeSpan.FromMinutes(1));
        string second = await service.GetTokenAsync(cancellationToken);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task RenewsTokenInsideRenewalSkew()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var service = CreateService(OwnerPrincipal(), time);
        var cancellationToken = TestContext.Current.CancellationToken;

        string first = await service.GetTokenAsync(cancellationToken);
        // Advance to just inside the renewal-skew window (remaining lifetime < RenewalSkew): renew.
        time.Advance(ApiJwtDefaults.TokenLifetime - ApiJwtDefaults.RenewalSkew + TimeSpan.FromSeconds(1));
        string renewed = await service.GetTokenAsync(cancellationToken);

        Assert.NotEqual(first, renewed);
    }

    [Fact]
    public async Task RoleDefaultsToMemberWhenRoleClaimAbsent()
    {
        var principal = Principal(
            new Claim(ClaimTypes.NameIdentifier, UserId),
            new Claim(TenantClaimTypes.TenantId, TenantId.ToString()));
        var service = CreateService(principal);

        string token = await service.GetTokenAsync(TestContext.Current.CancellationToken);

        var result = await ValidateAsync(token);
        Assert.Equal(TenantRoleNames.Member, GetClaim(result, TenantClaimTypes.TenantRole));
    }

    [Fact]
    public async Task ThrowsWhenUserHasNoTenantClaim()
    {
        var service = CreateService(Principal(new Claim(ClaimTypes.NameIdentifier, UserId)));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetTokenAsync(TestContext.Current.CancellationToken));
        Assert.Contains("tenant", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ThrowsWhenUserHasNoIdClaim()
    {
        var service = CreateService(Principal(new Claim(TenantClaimTypes.TenantId, TenantId.ToString())));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetTokenAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ThrowsActionableErrorWhenSigningKeyMissing()
    {
        var service = CreateService(OwnerPrincipal(), configuration: EmptyConfiguration());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetTokenAsync(TestContext.Current.CancellationToken));
        Assert.Contains(ApiJwtDefaults.SigningKeyConfigurationKey, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThrowsActionableErrorWhenSigningKeyIsNotBase64()
    {
        var service = CreateService(OwnerPrincipal(), configuration: Configuration("not-base64!"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetTokenAsync(TestContext.Current.CancellationToken));
        Assert.Contains("base64", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static TenantTokenService CreateService(
        ClaimsPrincipal principal, TimeProvider? timeProvider = null, IConfiguration? configuration = null) =>
        new(new FakeAuthenticationStateProvider(principal),
            configuration ?? Configuration(TestTokens.JwtSigningKey),
            timeProvider ?? TimeProvider.System);

    private static ClaimsPrincipal OwnerPrincipal() => Principal(
        new Claim(ClaimTypes.NameIdentifier, UserId),
        new Claim(TenantClaimTypes.TenantId, TenantId.ToString()),
        new Claim(TenantClaimTypes.TenantRole, TenantRoleNames.Owner));

    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Test"));

    private static IConfiguration Configuration(string signingKey) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [ApiJwtDefaults.SigningKeyConfigurationKey] = signingKey,
            })
            .Build();

    private static IConfiguration EmptyConfiguration() => new ConfigurationBuilder().Build();

    /// <summary>The exact parameters the API's JwtBearer enforces, via the shared factory.</summary>
    private static async Task<TokenValidationResult> ValidateAsync(string token) =>
        await new JsonWebTokenHandler().ValidateTokenAsync(token,
            ApiJwtDefaults.CreateValidationParameters(Convert.FromBase64String(TestTokens.JwtSigningKey)));

    private static string? GetClaim(TokenValidationResult result, string type) =>
        result.ClaimsIdentity.FindFirst(type)?.Value;

    private sealed class FakeAuthenticationStateProvider(ClaimsPrincipal principal) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(principal));
    }
}
