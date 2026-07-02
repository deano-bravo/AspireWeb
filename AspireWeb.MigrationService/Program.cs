using AspireWeb.Data;
using AspireWeb.Data.Tenancy;
using AspireWeb.MigrationService;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDataSource("appdb");

// Keep the Identity model at the same schema version the Web host uses (passkey-capable v3).
builder.Services.Configure<IdentityOptions>(options => options.Stores.SchemaVersion = IdentitySchemaVersions.Version3);

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
