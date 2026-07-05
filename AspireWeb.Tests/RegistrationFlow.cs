using System.Text.RegularExpressions;

namespace AspireWeb.Tests;

/// <summary>
/// Drives the real Blazor Register page over HTTP (fetch, re-post hidden + visible fields) and
/// reads back the signed-in principal's claims via the dev-only /debug/claims endpoint.
/// </summary>
internal static partial class RegistrationFlow
{
    /// <summary>Meets the server password policy (PasswordPolicy.MinimumLength).</summary>
    public const string DefaultPassword = "Sup3r-Secret-Pass!42";

    /// <summary>
    /// Fetches the Register page, re-posts every hidden field (antiforgery token, form handler)
    /// plus the visible inputs, then proves the client is signed in by returning the principal's
    /// claims from the dev-only /debug/claims endpoint.
    /// </summary>
    public static async Task<IReadOnlyList<TestClaim>> RegisterAsync(
        HttpClient client, string organization, string email, string password, CancellationToken cancellationToken)
    {
        string page = await client.GetStringAsync("/Account/Register", cancellationToken);

        var form = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match hidden in HiddenInputRegex().Matches(page))
        {
            var name = NameAttributeRegex().Match(hidden.Value);
            if (name.Success)
            {
                var value = ValueAttributeRegex().Match(hidden.Value);
                form[name.Groups["name"].Value] = value.Success ? value.Groups["value"].Value : "";
            }
        }

        form["Input.OrganizationName"] = organization;
        form["Input.Email"] = email;
        form["Input.Password"] = password;
        form["Input.ConfirmPassword"] = password;

        string action = FormActionRegex().Match(page) is { Success: true } match && match.Groups["action"].Value.Length > 0
            ? match.Groups["action"].Value
            : "/Account/Register";

        using var content = new FormUrlEncodedContent(form);
        using var response = await client.PostAsync(action, content, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await GetClaimsAsync(client, cancellationToken);
    }

    /// <summary>Reads the signed-in principal's claims via the dev-only /debug/claims endpoint.</summary>
    public static async Task<IReadOnlyList<TestClaim>> GetClaimsAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var claims = await client.GetFromJsonAsync<List<TestClaim>>("/debug/claims", cancellationToken);
        Assert.NotNull(claims);
        return claims;
    }

    public static string? GetClaim(IReadOnlyList<TestClaim> claims, string type) =>
        claims.FirstOrDefault(claim => claim.Type == type)?.Value;

    public static string UniqueOrganization(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    [GeneratedRegex("<input\\b[^>]*type=\"hidden\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex HiddenInputRegex();

    [GeneratedRegex("name=\"(?<name>[^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex NameAttributeRegex();

    [GeneratedRegex("value=\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex ValueAttributeRegex();

    [GeneratedRegex("<form\\b[^>]*method=\"post\"[^>]*action=\"(?<action>[^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex FormActionRegex();
}
