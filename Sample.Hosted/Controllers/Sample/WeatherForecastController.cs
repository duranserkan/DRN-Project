using DRN.Framework.Hosting.Nexus;
using DRN.Framework.Utils.Models;
using DRN.Framework.Utils.Models.Sample;

namespace Sample.Hosted.Controllers.Sample;


[ApiController]
[Route("Api/Sample/[controller]")]
public class WeatherForecastController(INexusClient nexusClient) : ControllerBase
{
    [HttpGet]
    public IEnumerable<WeatherForecast> Get() => WeatherForecast.Get();

    [HttpGet("Nexus")]
    public async Task<WeatherForecast[]?> GetNexusWeatherForecasts()
    {
        var response = await nexusClient.GetWeatherForecastAsync();

        return response.Payload;
    }
}