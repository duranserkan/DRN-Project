using DRN.Framework.Hosting.HealthCheck;

namespace DRN.Nexus.Hosted.Controllers.Sample;

[Route(NexusEndpointFor.ControllerRouteTemplate)]
public class WeatherForecastController : WeatherForecastControllerBase
{
    [HttpGet("private")]
    public ActionResult Private() => Ok("authorized");
}