using System.Text;

namespace AspireWeb.Data.Tenancy;

public static class TenantSlug
{
    /// <summary>
    /// Normalizes an organisation name to a unique slug: lowercase, ASCII letters/digits,
    /// dashes between words. Non-ASCII-only names fall back to a random slug rather than failing.
    /// </summary>
    public static string Normalize(string organizationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organizationName);

        var builder = new StringBuilder(organizationName.Length);
        bool lastWasDash = false;

        foreach (char character in organizationName.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(character);
                lastWasDash = false;
            }
            else if (!lastWasDash && builder.Length > 0)
            {
                builder.Append('-');
                lastWasDash = true;
            }
        }

        string slug = builder.ToString().TrimEnd('-');
        return slug.Length > 0 ? slug : $"org-{Guid.NewGuid():N}"[..12];
    }
}
