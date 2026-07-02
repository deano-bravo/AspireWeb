namespace AspireWeb.Data.Entities;

/// <summary>
/// The user's role within their tenant, stored as a column on the user and surfaced
/// as the "tenant_role" claim. Deliberately not ASP.NET Identity roles: AspNetRoles is
/// a global table, and a plain claim keeps cookie and API-JWT principals shape-identical.
/// </summary>
public enum TenantRole
{
    Member = 0,
    Admin = 1,
    Owner = 2,
}
