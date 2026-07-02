using AspireWeb.Data;
using AspireWeb.Data.Tenancy;
using AspireWeb.MigrationService;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDataSource("appdb");

builder.Services.AddSingleton<ITenantContext, UnscopedTenantContext>();
builder.Services.AddDbContext<ApplicationDbContext>((provider, options) =>
    options.UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>(), npgsql =>
        npgsql.MigrationsHistoryTable(ApplicationDbContext.MigrationsHistoryTableName)
            .EnableRetryOnFailure()));
builder.Services.AddDbContext<AppDbContext>((provider, options) =>
    options.UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>(), npgsql =>
        npgsql.MigrationsHistoryTable(AppDbContext.MigrationsHistoryTableName)
            .EnableRetryOnFailure()));

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await host.RunAsync();
