using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;

namespace AspireWeb.ServiceDefaults;

public static class TenantAuthorizationExtensions
{
    /// <summary>
    /// Tenant policies over the shared claims contract. Both the cookie principal (Web)
    /// and the JWT principal (API) carry the same claim shapes, so one policy set serves both.
    /// </summary>
    public static IServiceCollection AddTenantPolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(TenantPolicies.RequireTenant, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context =>
                    Guid.TryParse(context.User.FindFirstValue(TenantClaimTypes.TenantId), out _)))
            .AddPolicy(TenantPolicies.RequireTenantAdmin, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(TenantClaimTypes.TenantRole, TenantRoleNames.Admin, TenantRoleNames.Owner))
            .AddPolicy(TenantPolicies.RequireTenantOwner, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(TenantClaimTypes.TenantRole, TenantRoleNames.Owner));

        return services;
    }
}
