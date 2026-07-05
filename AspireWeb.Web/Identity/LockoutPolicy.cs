namespace AspireWeb.Web.Identity;

/// <summary>
/// Account lockout thresholds, following <see cref="PasswordPolicy"/>'s single-source approach
/// so the Identity options don't carry inline magic numbers.
/// </summary>
public static class LockoutPolicy
{
    public const int MaxFailedAttempts = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
}
