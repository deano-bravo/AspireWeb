using Microsoft.AspNetCore.Identity;

namespace AspireWeb.Data;

/// <summary>Identity store settings that must agree everywhere the Identity model is built.</summary>
public static class AppIdentityDefaults
{
    /// <summary>
    /// Passkey-capable schema (v3). The Web host, the migration service, and the design-time
    /// factory all build the Identity model from this one value — a mismatch would silently
    /// desync the runtime model from the generated migrations.
    /// </summary>
    public static readonly Version StoreSchemaVersion = IdentitySchemaVersions.Version3;
}
