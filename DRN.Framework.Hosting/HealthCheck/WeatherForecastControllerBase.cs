// This file is licensed to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using DRN.Framework.Utils.Models.Sample;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DRN.Framework.Hosting.HealthCheck;

[ApiController]
public abstract class WeatherForecastControllerBase : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [SuppressMessage("Performance", "CA1822", Justification = "MVC discovers the inherited HTTP GET action as an instance method.")]
    public IEnumerable<WeatherForecast> Get() => WeatherForecast.Get();
}
