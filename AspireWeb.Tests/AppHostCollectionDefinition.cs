namespace AspireWeb.Tests;

/// <summary>
/// Collection for tests that need the running AppHost (Docker). A collection fixture is
/// only instantiated when a selected test belongs to the collection — unlike an assembly
/// fixture, which xunit.v3 creates eagerly whenever any test runs, so a trait-filtered
/// fast run would still boot Docker.
/// </summary>
[CollectionDefinition(Name)]
public sealed class AppHostCollectionDefinition : ICollectionFixture<AppFixture>
{
    public const string Name = "AppHost";
}
