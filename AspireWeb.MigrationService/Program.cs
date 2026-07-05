using AspireWeb.Data;
using AspireWeb.Data.Tenancy;
using AspireWeb.MigrationService;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDataSource(DatabaseNames.AppDatabase);

builder.Services.AddSingleton<ITenantContext, UnscopedTenantContext>();
builder.Services.AddIdentityDbContext();
builder.Services.AddTenantDbContext();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await host.RunAsync();
