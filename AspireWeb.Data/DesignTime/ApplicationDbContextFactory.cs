using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace AspireWeb.Data.DesignTime;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // IdentityDbContext reads Stores.SchemaVersion from the application service provider,
        // and the shared schema version must match the runtime configuration in the Web host.
        var services = new ServiceCollection();
        services.Configure<IdentityOptions>(options => options.Stores.SchemaVersion = AppIdentityDefaults.StoreSchemaVersion);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                DesignTimeConnection.ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable(ApplicationDbContext.MigrationsHistoryTableName))
            .UseApplicationServiceProvider(services.BuildServiceProvider())
            .Options;

        return new ApplicationDbContext(options);
    }
}
