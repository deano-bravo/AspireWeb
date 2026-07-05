using AspireWeb.ApiService.Endpoints;
using AspireWeb.ApiService.Tenancy;
using AspireWeb.Data;
using AspireWeb.Data.Tenancy;
using AspireWeb.ServiceDefaults.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Tenant-scoped data access: tenant resolved from the validated JWT, reads isolated by
// the named query filter, writes stamped/guarded by the interceptor.
builder.AddNpgsqlDataSource(DatabaseNames.AppDatabase);
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ITenantContext, ClaimsTenantContext>();
builder.Services.AddScoped<ActiveTenantGate>();
builder.Services.AddTenantDbContext();

// Bearer auth for the self-issued web-to-api JWT (see ApiJwtDefaults for the contract).
byte[] signingKeyBytes = ApiJwtDefaults.GetSigningKeyBytes(builder.Configuration);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep raw claim names (sub / tenant_id) — the shared contract with the web front end.
        options.MapInboundClaims = false;
        // Tokens are validated by key; there is no metadata endpoint, and in-cluster traffic is HTTP.
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = ApiJwtDefaults.CreateValidationParameters(signingKeyBytes);
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

app.MapGet("/", () => "API service is running. Navigate to /weatherforecast to see sample data.")
    .AllowAnonymous();

app.MapWeatherEndpoints();
app.MapTodoEndpoints();

app.MapDefaultEndpoints();

await app.RunAsync();
