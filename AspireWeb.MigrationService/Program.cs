using AspireWeb.Data;
using AspireWeb.Data.Tenancy;
using AspireWeb.MigrationService;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDataSource("appdb");

builder.Services.AddSingleton<ITenantContext, UnscopedTenantContext>();
builder.Services.AddIdentityDbContext();
builder.Services.AddAppDbContext();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await host.RunAsync();
