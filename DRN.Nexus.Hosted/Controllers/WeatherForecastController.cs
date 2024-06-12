using DRN.Framework.Utils.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DRN.Nexus.Hosted.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    [HttpGet(Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast> Get() => WeatherForecast.Get();

    [HttpGet("private")]
    [Authorize]
    public ActionResult Private() => Ok("authorized");
}