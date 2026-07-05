using AspireWeb.Contracts;

namespace AspireWeb.Web.Clients;

public sealed class WeatherApiClient(HttpClient httpClient)
{
    // The forecast endpoint is anonymous and returns a small fixed array, so read it in one shot.
    public async Task<WeatherForecast[]> GetWeatherAsync(CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<WeatherForecast[]>("/weatherforecast", cancellationToken) ?? [];
}
