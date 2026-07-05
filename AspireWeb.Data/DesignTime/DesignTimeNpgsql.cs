using Microsoft.EntityFrameworkCore;

namespace AspireWeb.Data.DesignTime;

/// <summary>
/// Shared Npgsql wiring for the two <c>IDesignTimeDbContextFactory</c> implementations, so the
/// design-time connection source and history-table setup cannot drift apart between them.
/// </summary>
internal static class DesignTimeNpgsql
{
    public static DbContextOptionsBuilder<TContext> UseDesignTimeNpgsql<TContext>(
        this DbContextOptionsBuilder<TContext> options, string migrationsHistoryTable)
        where TContext : DbContext =>
        options.UseNpgsql(
            DesignTimeConnection.ConnectionString,
            npgsql => npgsql.MigrationsHistoryTable(migrationsHistoryTable));
}
