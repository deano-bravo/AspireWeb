using AspireWeb.Data;
using AspireWeb.Data.Entities;
using AspireWeb.ServiceDefaults.Tenancy;
using AspireWeb.Web.Components.Account;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

namespace AspireWeb.Web.Identity;

/// <summary>
/// Wires ASP.NET Core Identity, cookie auth, the shared tenant policies, and DataProtection,
/// split out of Program.cs alongside <c>ApiClientServiceCollectionExtensions.AddApiClients</c>.
/// </summary>
public static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddWebIdentity(this IServiceCollection services)
    {
        services.AddCascadingAuthenticationState();
        services.AddScoped<IdentityRedirectManager>();
        services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
        services.AddScoped<TenantProvisioningService>();
        // Registers the shared tenant policies (AuthorizeView on /todos resolves RequireTenantAdmin
        // by name — an unregistered policy throws at render time). Includes AddAuthorization().
        services.AddTenantPolicies();

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();

        services.AddIdentityDbContext();
        services.AddDatabaseDeveloperPageExceptionFilter();

        services.AddIdentityCore<ApplicationUser>(ConfigureIdentityOptions)
            .AddEntityFrameworkStores<AppIdentityDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddClaimsPrincipalFactory<TenantClaimsPrincipalFactory>();

        services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

        services.ConfigureApplicationCookie(options =>
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
        // within this shared interval (IdentityRevalidatingAuthenticationStateProvider reads the same value).
        services.Configure<SecurityStampValidatorOptions>(options =>
            options.ValidationInterval = SecurityStampDefaults.ValidationInterval);

        // Cookies and antiforgery tokens survive pod restarts/replicas: keys live in the database.
        // Production note: keys are stored unencrypted; add ProtectKeysWith* + a cert when it matters.
        services.AddDataProtection()
            .PersistKeysToDbContext<AppIdentityDbContext>()
            .SetApplicationName("AspireWeb");

        return services;
    }

    private static void ConfigureIdentityOptions(IdentityOptions options)
    {
        // Dev scaffold: flip to true (with a real IEmailSender) for production.
        // Stores.SchemaVersion is owned by AddIdentityDbContext — do not set it here.
        options.SignIn.RequireConfirmedAccount = false;
        // NIST-style password policy: length over composition rules (shared with the Register form).
        options.Password.RequiredLength = PasswordPolicy.MinimumLength;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireDigit = false;
        options.Lockout.MaxFailedAccessAttempts = LockoutPolicy.MaxFailedAttempts;
        options.Lockout.DefaultLockoutTimeSpan = LockoutPolicy.LockoutDuration;
        options.Lockout.AllowedForNewUsers = true;
    }
}
