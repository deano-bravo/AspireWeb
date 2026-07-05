using System.Security.Claims;
using AspireWeb.Data;
using AspireWeb.Data.Entities;
using AspireWeb.ServiceDefaults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AspireWeb.Web.Identity;

/// <summary>
/// Bakes the tenant claims into the cookie principal at sign-in. Claims refresh when the
/// security stamp is revalidated (5-minute interval) or on SignInManager.RefreshSignInAsync.
/// </summary>
public sealed class TenantClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    IOptions<IdentityOptions> optionsAccessor,
    ApplicationDbContext dbContext)
    : UserClaimsPrincipalFactory<ApplicationUser>(userManager, optionsAccessor)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(new Claim(TenantClaimTypes.TenantId, user.TenantId.ToString()));
        identity.AddClaim(new Claim(TenantClaimTypes.TenantRole, user.TenantRole.ToString()));

        string? tenantName = await dbContext.Tenants
            .Where(tenant => tenant.Id == user.TenantId)
            .Select(tenant => tenant.Name)
            .FirstOrDefaultAsync();
        if (tenantName is not null)
        {
            identity.AddClaim(new Claim(TenantClaimTypes.TenantName, tenantName));
        }

        if (!string.IsNullOrEmpty(user.DisplayName))
        {
            identity.AddClaim(new Claim(TenantClaimTypes.DisplayName, user.DisplayName));
        }

        return identity;
    }
}
