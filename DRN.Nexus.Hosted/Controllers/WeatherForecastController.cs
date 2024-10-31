using DRN.Framework.Utils.Models.Sample;

namespace DRN.Nexus.Hosted.Controllers;

[ApiController]
[Route(EndpointFor.ControllerRouteTemplate)]
public class WeatherForecastController : ControllerBase
{
    [HttpGet(Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast> Get() => WeatherForecast.Get();

    [HttpGet("private")]
    [Authorize]
    public ActionResult Private() => Ok("authorized");
}