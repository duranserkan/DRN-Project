using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sample.Domain.QA.Questions;
using Sample.Infra.QA;

namespace Sample.Hosted.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private readonly QAContext _context;

    public WeatherForecastController(QAContext context)
    {
        _context = context;
    }

    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };


    [HttpGet(Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast> Get()
    {
        return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
    }

    [HttpGet(Name = "Injection")]
    public async Task<IEnumerable<Question>> Get([FromQuery] string x)
    {
        var results = await _context.Questions
            .FromSqlRaw("SELECT * FROM qa.question WHERE Title = '" + x + "'")
            .ToListAsync();

        return results;
    }
}