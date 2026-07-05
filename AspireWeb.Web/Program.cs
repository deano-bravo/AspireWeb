using System.Security.Claims;
using AspireWeb.Data;
using AspireWeb.Web.Clients;
using AspireWeb.Web.Components;
using AspireWeb.Web.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();

// Each host keeps its own Aspire data-source registration; the DbContext wiring lives in
// AspireWeb.Data's AddIdentityDbContext (invoked by AddWebIdentity).
builder.AddNpgsqlDataSource(DatabaseNames.AppDatabase);

// Cross-cutting clock (injected by TenantTokenService and TenantProvisioningService).
builder.Services.AddSingleton(TimeProvider.System);

// Composition split out of this file (see ServiceCollectionExtensions).
builder.Services.AddApiClients();
builder.Services.AddWebIdentity();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

if (app.Environment.IsDevelopment())
{
    // Lets tests and developers inspect the cookie principal (tenant claims included).
    app.MapGet("/debug/claims", (ClaimsPrincipal user) =>
            Results.Json(user.Claims.Select(claim => new { claim.Type, claim.Value })))
        .RequireAuthorization();
}

await app.RunAsync();
