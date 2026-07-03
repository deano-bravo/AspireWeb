using AspireWeb.Data.Tenancy;

namespace AspireWeb.ApiService.Tenancy;

/// <summary>Rejects requests whose resolved tenant is missing or deactivated (403).</summary>
public sealed class RequireActiveTenantFilter(ITenantContext tenantContext, ActiveTenantGate gate) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        return tenantContext.TenantId is { } tenantId
            && await gate.IsActiveAsync(tenantId, context.HttpContext.RequestAborted)
                ? await next(context)
                : Results.Forbid();
    }
}
