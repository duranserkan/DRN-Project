// This file is licensed to you under the MIT license.

namespace DRN.Framework.Utils.Models.Sample;

public readonly struct WeatherForecast()
{
    public static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    public static WeatherForecast[] Get() => Enumerable.Range(1, 5).Select(index => new WeatherForecast
    {
        Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index))
    }).ToArray();

    public DateOnly Date { get; init; } = DateOnly.FromDateTime(DateTime.Now);
    public int TemperatureC { get; init; } = Random.Shared.Next(-20, 55);
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
    public string Summary { get; init; } = Summaries[Random.Shared.Next(Summaries.Length)];
}