using DRN.Framework.Hosting.HealthCheck;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DRN.Nexus.Hosted.Controllers.Sample;

[Route(NexusEndpointFor.ControllerRouteTemplate)]
public class WeatherForecastController : WeatherForecastControllerBase
{
    [HttpGet("private")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public ActionResult Private() => Ok("authorized");
}