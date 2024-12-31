// This file is licensed to you under the MIT license.

using DRN.Framework.Utils.Models.Sample;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DRN.Framework.Hosting.HealthCheck;

[ApiController]
public abstract class WeatherForecastControllerBase : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public IEnumerable<WeatherForecast> Get() => WeatherForecast.Get();
}