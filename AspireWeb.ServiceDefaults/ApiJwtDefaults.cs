using Microsoft.Extensions.Configuration;

namespace AspireWeb.ServiceDefaults;

/// <summary>
/// The contract for the short-lived JWT the web front end mints to call the API service
/// (trusted-subsystem propagation of user + tenant). The symmetric signing key flows from
/// the AppHost "jwt-signing-key" secret parameter to both services.
/// Upgrade path when more clients appear: asymmetric keys (JWKS), then an external IdP —
/// the claims contract (sub, tenant_id, tenant_role) stays fixed.
/// </summary>
public static class ApiJwtDefaults
{
    public const string Issuer = "aspireweb-web";
    public const string Audience = "aspireweb-api";
    public const string SigningKeyConfigurationKey = "Auth:ApiJwt:SigningKey";

    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(1);

    /// <summary>HS256 floor: a shorter key weakens the signature below the algorithm's strength.</summary>
    private const int MinimumKeyLengthBytes = 32;

    /// <summary>
    /// Reads and validates the shared signing key. Both the minting side (Web) and the
    /// validating side (API) use this, so a malformed secret fails fast with an actionable
    /// message instead of a raw FormatException.
    /// </summary>
    public static byte[] GetSigningKeyBytes(IConfiguration configuration)
    {
        string signingKey = configuration[SigningKeyConfigurationKey]
            ?? throw new InvalidOperationException(
                $"Configuration '{SigningKeyConfigurationKey}' is required. " +
                "Provide the AppHost 'jwt-signing-key' secret parameter (dotnet user-secrets on the AppHost project).");

        byte[] keyBytes;
        try
        {
            keyBytes = Convert.FromBase64String(signingKey);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                $"Configuration '{SigningKeyConfigurationKey}' must be base64-encoded random bytes " +
                "(e.g. openssl rand -base64 32).",
                exception);
        }

        return keyBytes.Length >= MinimumKeyLengthBytes
            ? keyBytes
            : throw new InvalidOperationException(
                $"Configuration '{SigningKeyConfigurationKey}' must decode to at least " +
                $"{MinimumKeyLengthBytes} bytes for HS256; got {keyBytes.Length}.");
    }
}
