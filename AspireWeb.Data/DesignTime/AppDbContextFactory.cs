using AspireWeb.Data.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AspireWeb.Data.DesignTime;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                DesignTimeConnection.ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable(AppDbContext.MigrationsHistoryTableName))
            .Options;

        return new AppDbContext(options, new UnscopedTenantContext());
    }
}
