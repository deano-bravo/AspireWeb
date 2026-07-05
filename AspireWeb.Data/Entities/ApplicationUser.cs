using Microsoft.AspNetCore.Identity;

namespace AspireWeb.Data.Entities;

public sealed class ApplicationUser : IdentityUser
{
    /// <summary>Every user belongs to exactly one tenant.</summary>
    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    public TenantRole TenantRole { get; set; }

    public string? DisplayName { get; set; }
}
