using AspireWeb.Data.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AspireWeb.Data.DesignTime;

public sealed class TenantDbContextFactory : IDesignTimeDbContextFactory<TenantDbContext>
{
    public TenantDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseDesignTimeNpgsql(TenantDbContext.MigrationsHistoryTableName)
            .Options;

        return new TenantDbContext(options, new UnscopedTenantContext());
    }
}
