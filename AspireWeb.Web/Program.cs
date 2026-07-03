using System.Security.Claims;
using AspireWeb.Data;
using AspireWeb.Data.Entities;
using AspireWeb.Web;
using AspireWeb.Web.Components;
using AspireWeb.Web.Components.Account;
using AspireWeb.Web.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();

builder.Services.AddHttpClient<WeatherApiClient>(client =>
    {
        // This URL uses "https+http://" to indicate HTTPS is preferred over HTTP.
        // Learn more about service discovery scheme resolution at https://aka.ms/dotnet/sdschemes.
        client.BaseAddress = new("https+http://apiservice");
    });

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<TenantTokenService>();
builder.Services.AddHttpClient<TodoApiClient>(client =>
    {
        client.BaseAddress = new("https+http://apiservice");
    });

// Identity (cookie auth) with the Blazor template's Account components.
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddAuthorization();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

builder.AddNpgsqlDataSource("appdb");
builder.Services.AddDbContext<ApplicationDbContext>((provider, options) =>
    options.UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>(), npgsql =>
        npgsql.MigrationsHistoryTable(ApplicationDbContext.MigrationsHistoryTableName)
            .EnableRetryOnFailure()));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        // Dev scaffold: flip to true (with a real IEmailSender) for production.
        options.SignIn.RequireConfirmedAccount = false;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
        // NIST-style password policy: length over composition rules.
        options.Password.RequiredLength = 12;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireDigit = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders()
    .AddClaimsPrincipalFactory<TenantClaimsPrincipalFactory>();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "__AspireWeb.Auth";
    options.Cookie.HttpOnly = true;
    // TLS terminates at the ingress; ASPNETCORE_FORWARDEDHEADERS_ENABLED makes this safe in-cluster.
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// Tenant/role mutations bump the user's security stamp; cookies and circuits revalidate
// within this interval (IdentityRevalidatingAuthenticationStateProvider matches it).
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromMinutes(5));

// Cookies and antiforgery tokens survive pod restarts/replicas: keys live in the database.
// Production note: keys are stored unencrypted; add ProtectKeysWith* + a cert when it matters.
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>()
    .SetApplicationName("AspireWeb");

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
