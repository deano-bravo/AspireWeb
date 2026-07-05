using System.Security.Claims;
using AspireWeb.ServiceDefaults;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AspireWeb.Web.Identity;

/// <summary>
/// Mints the short-lived JWT that carries the signed-in user's identity and tenant to the
/// API service. Scoped and injected into typed HTTP clients directly — never via a
/// DelegatingHandler, because HttpClientFactory caches handler pipelines in their own DI
/// scope where circuit-scoped auth state is invisible.
/// </summary>
public sealed class TenantTokenService(
    AuthenticationStateProvider authenticationStateProvider,
    IConfiguration configuration,
    TimeProvider timeProvider)
{
    private string? _cachedToken;
    private DateTimeOffset _expiresAt;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        // Minting is CPU-only (no I/O to cancel), so honour cancellation up front.
        cancellationToken.ThrowIfCancellationRequested();

        var now = timeProvider.GetUtcNow();
        if (_cachedToken is not null && now < _expiresAt - ApiJwtDefaults.RenewalSkew)
        {
            return _cachedToken;
        }

        var user = (await authenticationStateProvider.GetAuthenticationStateAsync()).User;
        string tenantId = user.FindFirstValue(TenantClaimTypes.TenantId)
            ?? throw new InvalidOperationException("The current user has no tenant claim — sign in first.");
        string userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("The current user has no id claim — sign in first.");

        byte[] signingKeyBytes = ApiJwtDefaults.GetSigningKeyBytes(configuration);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = ApiJwtDefaults.Issuer,
            Audience = ApiJwtDefaults.Audience,
            IssuedAt = now.UtcDateTime,
            Expires = now.Add(ApiJwtDefaults.TokenLifetime).UtcDateTime,
            Claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [JwtRegisteredClaimNames.Sub] = userId,
                [TenantClaimTypes.TenantId] = tenantId,
                [TenantClaimTypes.TenantRole] =
                    user.FindFirstValue(TenantClaimTypes.TenantRole) ?? TenantRoleNames.Member,
            },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(signingKeyBytes),
                SecurityAlgorithms.HmacSha256),
        };

        _cachedToken = new JsonWebTokenHandler().CreateToken(descriptor);
        _expiresAt = now.Add(ApiJwtDefaults.TokenLifetime);
        return _cachedToken;
    }
}
