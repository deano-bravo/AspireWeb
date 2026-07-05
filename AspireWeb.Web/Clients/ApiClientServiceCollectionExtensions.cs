using AspireWeb.Web.Identity;

namespace AspireWeb.Web.Clients;

/// <summary>
/// Wires the typed API clients and the tenant token minter they carry, split out of Program.cs
/// alongside <c>IdentityServiceCollectionExtensions.AddWebIdentity</c>.
/// </summary>
public static class ApiClientServiceCollectionExtensions
{
    // "https+http://" prefers HTTPS over HTTP via Aspire service discovery.
    // Learn more at https://aka.ms/dotnet/sdschemes.
    private const string ApiServiceUrl = "https+http://apiservice";

    public static IServiceCollection AddApiClients(this IServiceCollection services)
    {
        // Scoped and injected into the typed client directly (never a DelegatingHandler — see
        // TenantTokenService): mints the short-lived bearer token that carries user + tenant.
        services.AddScoped<TenantTokenService>();

        services.AddHttpClient<WeatherApiClient>(client => client.BaseAddress = new(ApiServiceUrl));
        services.AddHttpClient<TodoApiClient>(client => client.BaseAddress = new(ApiServiceUrl));

        return services;
    }
}
