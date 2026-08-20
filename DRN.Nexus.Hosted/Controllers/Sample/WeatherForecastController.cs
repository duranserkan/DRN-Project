using DRN.Framework.Hosting.HealthCheck;

namespace DRN.Nexus.Hosted.Controllers.Sample;

[Route(SampleApiFor.ControllerRouteTemplate)]
public class WeatherForecastController : WeatherForecastControllerBase
{
    [HttpGet("private")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public ActionResult Private() => Ok("authorized");
}
