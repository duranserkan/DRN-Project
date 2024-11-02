using DRN.Framework.Utils.Models.Sample;

namespace DRN.Nexus.Hosted.Controllers.Sample;

[ApiController]
[Route(NexusEndpointFor.ControllerRouteTemplate)]
public class WeatherForecastController : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public IEnumerable<WeatherForecast> Get() => WeatherForecast.Get();

    [HttpGet("private")]
    public ActionResult Private() => Ok("authorized");
}