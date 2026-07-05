using Microsoft.AspNetCore.Identity;

namespace AspireWeb.Web.Identity;

/// <summary>Outcome of provisioning a tenant + owner; errors render like any Identity failure.</summary>
public sealed class TenantProvisioningResult
{
    private TenantProvisioningResult(bool succeeded, IReadOnlyList<IdentityError> errors)
    {
        Succeeded = succeeded;
        Errors = errors;
    }

    public bool Succeeded { get; }

    public IReadOnlyList<IdentityError> Errors { get; }

    public static TenantProvisioningResult Success() => new(succeeded: true, []);

    public static TenantProvisioningResult Failed(IEnumerable<IdentityError> errors) =>
        new(succeeded: false, [.. errors]);
}
