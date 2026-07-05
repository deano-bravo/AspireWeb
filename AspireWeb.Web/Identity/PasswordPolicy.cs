namespace AspireWeb.Web.Identity;

/// <summary>
/// NIST-style password policy: length over composition rules. Consumed by both the
/// Identity options (server enforcement) and the Register form's validation attributes —
/// consts so they are usable in attribute arguments.
/// </summary>
public static class PasswordPolicy
{
    public const int MinimumLength = 12;
    public const int MaximumLength = 100;
}
