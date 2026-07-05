namespace AspireWeb.Web.Identity;

/// <summary>
/// Single bound for how quickly tenant/role mutations (which bump the security stamp)
/// propagate to signed-in users: cookies revalidate via SecurityStampValidatorOptions and
/// long-lived circuits via IdentityRevalidatingAuthenticationStateProvider — both read this.
/// </summary>
public static class SecurityStampDefaults
{
    public static readonly TimeSpan ValidationInterval = TimeSpan.FromMinutes(5);
}
