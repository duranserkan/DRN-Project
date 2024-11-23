using DRN.Framework.Hosting.HealthCheck;
using DRN.Framework.Hosting.Nexus;
using DRN.Framework.Utils.Models.Sample;

namespace Sample.Hosted.Controllers.Sample;

[AllowAnonymous]
[Route(SampleApiFor.ControllerRouteTemplate)]
public class WeatherForecastController(INexusClient nexusClient) : WeatherForecastControllerBase
{
    [HttpGet("Nexus")]
    public async Task<WeatherForecast[]?> GetNexusWeatherForecasts()
    {
        var response = await nexusClient.GetWeatherForecastAsync();

        return response.Payload;
    }
}