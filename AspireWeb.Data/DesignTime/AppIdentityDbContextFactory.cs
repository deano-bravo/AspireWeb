using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace AspireWeb.Data.DesignTime;

public sealed class AppIdentityDbContextFactory : IDesignTimeDbContextFactory<AppIdentityDbContext>
{
    public AppIdentityDbContext CreateDbContext(string[] args)
    {
        // IdentityDbContext reads Stores.SchemaVersion from the application service provider,
        // and the shared schema version must match the runtime configuration in the Web host.
        var services = new ServiceCollection();
        services.Configure<IdentityOptions>(options => options.Stores.SchemaVersion = AppIdentityDefaults.StoreSchemaVersion);

        var options = new DbContextOptionsBuilder<AppIdentityDbContext>()
            .UseDesignTimeNpgsql(AppIdentityDbContext.MigrationsHistoryTableName)
            .UseApplicationServiceProvider(services.BuildServiceProvider())
            .Options;

        return new AppIdentityDbContext(options);
    }
}
