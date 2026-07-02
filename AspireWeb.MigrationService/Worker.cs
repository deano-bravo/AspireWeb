using AspireWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace AspireWeb.MigrationService;

/// <summary>
/// Applies EF Core migrations for both contexts, then stops. The AppHost gates the
/// web and API services on this resource via WaitForCompletion, so a failed migration
/// (non-zero exit code) blocks startup instead of running against a stale schema.
/// </summary>
public sealed partial class Worker(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime lifetime,
    ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();

            // Identity context first: it owns the Tenants table that AppDbContext's FKs target.
            await MigrateAsync<ApplicationDbContext>(scope.ServiceProvider, stoppingToken);
            await MigrateAsync<AppDbContext>(scope.ServiceProvider, stoppingToken);

            LogMigrationsApplied(logger);
        }
        catch (Exception exception)
        {
            LogMigrationFailed(logger, exception);
            Environment.ExitCode = 1;
        }

        lifetime.StopApplication();
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
