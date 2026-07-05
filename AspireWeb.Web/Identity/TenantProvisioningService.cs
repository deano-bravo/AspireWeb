using AspireWeb.Data;
using AspireWeb.Data.Entities;
using AspireWeb.Data.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AspireWeb.Web.Identity;

/// <summary>
/// Creates a tenant and its owner user atomically: a failed user creation must not leave
/// an orphan tenant. Extracted from the Register page so the flow is reusable and testable.
/// </summary>
public sealed class TenantProvisioningService(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager)
{
    /// <summary>
    /// The caller prepares <paramref name="owner"/> (username/email already set); this method
    /// owns the tenant insert, the owner-role assignment, and the surrounding transaction.
    /// UserManager shares the scoped DbContext, so CreateAsync participates in it.
    /// </summary>
    public async Task<TenantProvisioningResult> ProvisionTenantOwnerAsync(
        string organizationName,
        ApplicationUser owner,
        string password,
        CancellationToken cancellationToken = default)
    {
        // The execution strategy wrapper is required because retries are enabled.
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear(); // keep retried attempts idempotent

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = organizationName.Trim(),
                Identifier = TenantSlug.Normalize(organizationName),
                CreatedAt = DateTimeOffset.UtcNow,
            };
            dbContext.Tenants.Add(tenant);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (PostgresErrors.IsUniqueViolation(exception))
            {
                // The unique index on Tenants.Identifier is the enforcement; this is just the friendly message.
                return TenantProvisioningResult.Failed(
                    [new IdentityError { Code = "DuplicateOrganization", Description = "That organisation name is already taken." }]);
            }

            owner.TenantId = tenant.Id;
            owner.TenantRole = TenantRole.Owner;

            var result = await userManager.CreateAsync(owner, password);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return TenantProvisioningResult.Failed(result.Errors);
            }

            await transaction.CommitAsync(cancellationToken);
            return TenantProvisioningResult.Success();
        });
    }
}
