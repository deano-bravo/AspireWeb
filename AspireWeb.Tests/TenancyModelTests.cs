using AspireWeb.Data;
using AspireWeb.Data.Entities;
using AspireWeb.Data.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace AspireWeb.Tests;

/// <summary>
/// Model-shape and write-guard tests for the tenancy layer. These run without any
/// database or AppHost (EF InMemory / model-only), so they stay fast.
/// </summary>
public class TenancyModelTests
{
    [Fact]
    public void EveryTenantOwnedEntityHasTheNamedTenantFilter()
    {
        using var context = CreateModelOnlyContext();

        var tenantOwned = context.Model.GetEntityTypes()
            .Where(entityType => typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            .ToList();

        Assert.NotEmpty(tenantOwned);
        Assert.All(tenantOwned, entityType =>
            Assert.Contains(entityType.GetDeclaredQueryFilters(),
                filter => filter.Key == AppDbContext.TenantFilterName));
    }

    [Fact]
    public async Task SaveChangesStampsTheAmbientTenantOnNewEntities()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateInMemoryContext(new FixedTenantContext(tenantId), Guid.NewGuid().ToString());

        var item = NewItem("stamped");
        context.TodoItems.Add(item);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(tenantId, item.TenantId);
    }

    [Fact]
    public async Task SaveChangesBlocksCrossTenantWrites()
    {
        using var context = CreateInMemoryContext(new FixedTenantContext(Guid.NewGuid()), Guid.NewGuid().ToString());

        var foreignItem = NewItem("foreign");
        foreignItem.TenantId = Guid.NewGuid(); // some other tenant
        context.TodoItems.Add(foreignItem);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveChangesRequiresAnAmbientTenantForTenantOwnedWrites()
    {
        using var context = CreateInMemoryContext(new UnscopedTenantContext(), Guid.NewGuid().ToString());

        context.TodoItems.Add(NewItem("orphan"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task QueryFilterIsolatesTenantsPerContextInstance()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        string store = Guid.NewGuid().ToString();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        using (var contextA = CreateInMemoryContext(new FixedTenantContext(tenantA), store))
        {
            contextA.TodoItems.Add(NewItem("belongs to A"));
            await contextA.SaveChangesAsync(cancellationToken);
        }

        // The filter must re-bind to each context instance's tenant, not the first one that
        // built the (cached) model.
        using (var contextB = CreateInMemoryContext(new FixedTenantContext(tenantB), store))
        {
            Assert.Empty(await contextB.TodoItems.ToListAsync(cancellationToken));
        }

        using var contextA2 = CreateInMemoryContext(new FixedTenantContext(tenantA), store);
        Assert.Single(await contextA2.TodoItems.ToListAsync(cancellationToken));
    }

    private static AppDbContext CreateModelOnlyContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=localhost;Database=model-only;Username=model")
                .Options,
            new UnscopedTenantContext());

    private static AppDbContext CreateInMemoryContext(ITenantContext tenantContext, string storeName) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(storeName)
                .AddInterceptors(new TenantSaveChangesInterceptor(tenantContext))
                .Options,
            tenantContext);

    private static TodoItem NewItem(string title) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        NormalizedTitle = title.ToUpperInvariant(),
        CreatedAt = DateTimeOffset.UtcNow,
    };
}
