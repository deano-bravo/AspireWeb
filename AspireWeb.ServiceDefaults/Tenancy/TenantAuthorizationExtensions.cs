using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace AspireWeb.ServiceDefaults.Tenancy;

public static class TenantAuthorizationExtensions
{
    /// <summary>
    /// Tenant policies over the shared claims contract. Both the cookie principal (Web)
    /// and the JWT principal (API) carry the same claim shapes, so one policy set serves both.
    /// Every policy requires a resolvable tenant; the admin/owner policies add the role gate on
    /// top, so they can never authorize a principal without a tenant.
    /// </summary>
    public static IServiceCollection AddTenantPolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(TenantPolicies.RequireTenant, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(HasTenant))
            .AddPolicy(TenantPolicies.RequireTenantAdmin, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(HasTenant)
                .RequireClaim(TenantClaimTypes.TenantRole, TenantRoleNames.Admin, TenantRoleNames.Owner))
            .AddPolicy(TenantPolicies.RequireTenantOwner, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(HasTenant)
                .RequireClaim(TenantClaimTypes.TenantRole, TenantRoleNames.Owner));

        return services;
    }

    private static bool HasTenant(AuthorizationHandlerContext context) =>
        context.User.GetTenantId() is not null;
}
