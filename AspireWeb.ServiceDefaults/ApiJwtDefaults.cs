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
}
