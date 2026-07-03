using AspireWeb.ApiService;
using AspireWeb.ApiService.Endpoints;
using AspireWeb.ApiService.Tenancy;
using AspireWeb.Data;
using AspireWeb.Data.Tenancy;
using AspireWeb.ServiceDefaults;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Tenant-scoped data access: tenant resolved from the validated JWT, reads isolated by
// the named query filter, writes stamped/guarded by the interceptor.
builder.AddNpgsqlDataSource("appdb");
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ITenantContext, ClaimsTenantContext>();
builder.Services.AddScoped<TenantSaveChangesInterceptor>();
builder.Services.AddScoped<ActiveTenantGate>();
builder.Services.AddDbContext<AppDbContext>((provider, options) =>
    options.UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>(), npgsql =>
            npgsql.MigrationsHistoryTable(AppDbContext.MigrationsHistoryTableName)
                .EnableRetryOnFailure())
        .AddInterceptors(provider.GetRequiredService<TenantSaveChangesInterceptor>()));

// Bearer auth for the self-issued web-to-api JWT (see ApiJwtDefaults for the contract).
string signingKey = builder.Configuration[ApiJwtDefaults.SigningKeyConfigurationKey]
    ?? throw new InvalidOperationException(
        $"Configuration '{ApiJwtDefaults.SigningKeyConfigurationKey}' is required. " +
        "Provide the AppHost 'jwt-signing-key' secret parameter (dotnet user-secrets on the AppHost project).");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep raw claim names (sub / tenant_id) — the shared contract with the web front end.
        options.MapInboundClaims = false;
        // Tokens are validated by key; there is no metadata endpoint, and in-cluster traffic is HTTP.
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = ApiJwtDefaults.Issuer,
            ValidAudience = ApiJwtDefaults.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(signingKey)),
            ClockSkew = ApiJwtDefaults.ClockSkew,
        };
    });

builder.Services.AddTenantPolicies();
// Unmapped endpoints fail closed; anonymous ones must opt out explicitly.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

string[] summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

app.MapGet("/", () => "API service is running. Navigate to /weatherforecast to see sample data.")
    .AllowAnonymous();

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.AllowAnonymous();

app.MapTodoEndpoints();

app.MapDefaultEndpoints();

await app.RunAsync();
