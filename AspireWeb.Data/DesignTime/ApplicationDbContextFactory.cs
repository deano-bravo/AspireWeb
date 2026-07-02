using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AspireWeb.Data.DesignTime;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                DesignTimeConnection.ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable(ApplicationDbContext.MigrationsHistoryTableName))
            .Options;

        return new ApplicationDbContext(options);
    }
}
