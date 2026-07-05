namespace AspireWeb.Tests;

/// <summary>
/// Smoke tests over the running AppHost: the web front end serves its root and the weather
/// API stays anonymous.
/// </summary>
[Collection(AppHostCollectionDefinition.Name)]
[Trait(TestCategories.TraitName, TestCategories.Integration)]
public class WebTests(AppFixture fixture)
{
    [Fact]
    public async Task WebRootRespondsWithOk()
    {
        using var httpClient = fixture.App.CreateHttpClient("webfrontend");

        var response = await httpClient.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WeatherApiRemainsAnonymous()
    {
        using var apiClient = fixture.App.CreateHttpClient("apiservice");

        var response = await apiClient.GetAsync("/weatherforecast", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
