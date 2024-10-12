using DRN.Framework.Hosting.Nexus;
using DRN.Framework.Utils.Models;

namespace Sample.Hosted.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController(INexusClient nexusClient) : ControllerBase
{
    [HttpGet]
    public IEnumerable<WeatherForecast> Get() => WeatherForecast.Get();

    [HttpGet("nexus")]
    public async Task<WeatherForecast[]?> GetNexusWeatherForecasts()
    {
        var response = await nexusClient.GetWeatherForecastAsync();

        return response.Payload;
    }
}