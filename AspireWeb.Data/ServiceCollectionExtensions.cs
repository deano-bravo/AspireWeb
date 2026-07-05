using AspireWeb.Data.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace AspireWeb.Data;

/// <summary>
/// Shared DbContext registration for every host (API, Web, migration service), so the
/// history-table names, retry policy, and tenancy interceptor cannot drift apart.
/// Hosts keep their own <c>builder.AddNpgsqlDataSource("appdb")</c> call — the Aspire
/// integration (config binding, health check, telemetry) belongs to the host; these
/// extensions resolve the resulting <see cref="NpgsqlDataSource"/> from DI.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the tenant-scoped <see cref="TenantDbContext"/> with its write-side guard.
    /// The host must also register an <see cref="ITenantContext"/> — the context's
    /// constructor requires one, so a missing registration fails fast at first resolve.
    /// </summary>
    public static IServiceCollection AddTenantDbContext(this IServiceCollection services)
    {
        services.TryAddScoped<TenantSaveChangesInterceptor>();
        services.AddDbContext<TenantDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>(), npgsql =>
                    npgsql.MigrationsHistoryTable(TenantDbContext.MigrationsHistoryTableName)
                        .EnableRetryOnFailure())
                .AddInterceptors(provider.GetRequiredService<TenantSaveChangesInterceptor>()));
        return services;
    }

    /// <summary>
    /// Registers <see cref="ApplicationDbContext"/> (Identity + tenant registry) and owns the
    /// Identity store schema-version pin — callers of <c>AddIdentityCore</c> must not set
    /// <c>Stores.SchemaVersion</c> again (both configure actions would run; this one must stand).
    /// </summary>
    public static IServiceCollection AddIdentityDbContext(this IServiceCollection services)
    {
        services.Configure<IdentityOptions>(options =>
            options.Stores.SchemaVersion = AppIdentityDefaults.StoreSchemaVersion);
        services.AddDbContext<ApplicationDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>(), npgsql =>
                npgsql.MigrationsHistoryTable(ApplicationDbContext.MigrationsHistoryTableName)
                    .EnableRetryOnFailure()));
        return services;
    }
}
