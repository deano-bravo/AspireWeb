using System.Net.Http.Headers;
using AspireWeb.ServiceDefaults.Tenancy;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AspireWeb.Tests;

/// <summary>
/// Test-side JWT minting and authenticated API requests. <see cref="MintJwt"/> deliberately
/// re-implements TenantTokenService's descriptor so tests can forge partial/wrong-key tokens —
/// keep the claim shape in sync with that service.
/// </summary>
internal static class TestTokens
{
    /// <summary>Deterministic signing key so tests can mint their own API tokens.</summary>
    public const string JwtSigningKey = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";

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

    /// <summary>A bearer-authenticated request for the API service.</summary>
    public static HttpRequestMessage ApiRequest(HttpMethod method, string uri, string token)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
