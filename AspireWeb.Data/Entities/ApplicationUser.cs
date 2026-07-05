using Microsoft.AspNetCore.Identity;

namespace AspireWeb.Data.Entities;

/// <summary>An identity user, extended with the tenant it belongs to and its role within that tenant.</summary>
public sealed class ApplicationUser : IdentityUser
{
    /// <summary>Max length of <see cref="DisplayName"/>.</summary>
    public const int DisplayNameMaxLength = 128;

    /// <summary>Every user belongs to exactly one tenant.</summary>
    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    public TenantRole TenantRole { get; set; }

    public string? DisplayName { get; set; }
}
