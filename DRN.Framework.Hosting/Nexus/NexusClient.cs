using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Http;
using DRN.Framework.Utils.Models;
using Flurl.Http;

namespace DRN.Framework.Hosting.Nexus;

public interface INexusClient
{
    Task<HttpResponse<string>> GetStatusAsync();
    Task<HttpResponse<WeatherForecast[]>> GetWeatherForecastAsync();
}

[Singleton<INexusClient>]
public class NexusClient(INexusRequest request) : INexusClient
{
    public async Task<HttpResponse<string>> GetStatusAsync() =>
        await request.For("status").GetAsync().ToStringAsync();

    public async Task<HttpResponse<WeatherForecast[]>> GetWeatherForecastAsync() =>
        await request.For("WeatherForecast").GetAsync().ToJsonAsync<WeatherForecast[]>();
}