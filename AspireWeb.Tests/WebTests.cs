namespace AspireWeb.Tests;

public class WebTests(AppFixture fixture)
{
    [Fact]
    public async Task GetWebResourceRootReturnsOkStatusCode()
    {
        // Arrange
        using var httpClient = fixture.App.CreateHttpClient("webfrontend");

        // Act
        var response = await httpClient.GetAsync("/", TestContext.Current.CancellationToken);

        // Assert
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
