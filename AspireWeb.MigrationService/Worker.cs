namespace AspireWeb.MigrationService;

public sealed class Worker(IHostApplicationLifetime lifetime) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        lifetime.StopApplication();
        return Task.CompletedTask;
    }
}
