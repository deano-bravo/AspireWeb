using AspireWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace AspireWeb.MigrationService;

/// <summary>
/// Applies EF Core migrations for both contexts. In Development (aspire run, tests) it then
/// stops, so the AppHost's WaitForCompletion gates the web and API services on a completed
/// migration; a failed migration exits non-zero and blocks startup. Outside Development
/// (the generated Kubernetes chart renders this as a Deployment) it idles after migrating,
/// because an exiting pod would restart-loop.
/// </summary>
public sealed partial class Worker(
    IServiceProvider serviceProvider,
    IHostEnvironment environment,
    IHostApplicationLifetime lifetime,
    ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();

            // Identity context first: it owns the Tenants table that TenantDbContext's FKs target.
            await MigrateAsync<ApplicationDbContext>(scope.ServiceProvider, stoppingToken);
            await MigrateAsync<TenantDbContext>(scope.ServiceProvider, stoppingToken);

            LogMigrationsApplied(logger);
        }
        catch (Exception exception)
        {
            LogMigrationFailed(logger, exception);
            Environment.ExitCode = 1;
            lifetime.StopApplication();
            return;
        }

        if (environment.IsDevelopment())
        {
            lifetime.StopApplication();
            return;
        }

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    private static async Task MigrateAsync<TContext>(IServiceProvider services, CancellationToken cancellationToken)
        where TContext : DbContext
    {
        var context = services.GetRequiredService<TContext>();
        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(() => context.Database.MigrateAsync(cancellationToken));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Database migrations applied")]
    private static partial void LogMigrationsApplied(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Applying database migrations failed")]
    private static partial void LogMigrationFailed(ILogger logger, Exception exception);
}
