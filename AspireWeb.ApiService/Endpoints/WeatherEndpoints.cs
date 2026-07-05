using AspireWeb.Contracts;

namespace AspireWeb.ApiService.Endpoints;

public static class WeatherEndpoints
{
    private static readonly string[] Summaries =
        ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

    /// <summary>
    /// The documented anonymous-endpoint exemplar (see CLAUDE.md conventions). Safe for the
    /// consuming Weather page to output-cache because it is not tenant data.
    /// </summary>
    public static IEndpointRouteBuilder MapWeatherEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/weatherforecast", GetForecast)
            .WithName("GetWeatherForecast")
            .WithTags("Weather")
            .Produces<WeatherForecast[]>()
            .AllowAnonymous();

        return endpoints;
    }

    private static WeatherForecast[] GetForecast() =>
        [.. Enumerable.Range(1, 5).Select(index => new WeatherForecast(
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            Summaries[Random.Shared.Next(Summaries.Length)]))];
}
