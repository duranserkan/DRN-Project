using DRN.Framework.Hosting.Nexus;
using DRN.Framework.Utils.Models;
using Microsoft.AspNetCore.Mvc;

namespace Sample.Hosted.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController(INexusClient nexusClient) : ControllerBase
{
    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    [HttpGet]
    public IEnumerable<WeatherForecast> Get() => Enumerable.Range(1, 5)
        .Select(index => new WeatherForecast
        {
            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        }).ToArray();

    [HttpGet("nexus")]
    public async Task<IEnumerable<WeatherForecast>?> GetNexusWeatherForecasts()
    {
        var response = await nexusClient.GetWeatherForecastAsync();

        return response.Payload;
    }
}