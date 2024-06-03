using DRN.Framework.Hosting.Nexus;
using DRN.Framework.Utils.Models;
using Microsoft.AspNetCore.Mvc;

namespace Sample.Hosted.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController(INexusClient nexusClient) : ControllerBase
{
    [HttpGet]
    public IEnumerable<WeatherForecast> Get() => WeatherForecast.Get;

    [HttpGet("nexus")]
    public async Task<WeatherForecast[]?> GetNexusWeatherForecasts()
    {
        var response = await nexusClient.GetWeatherForecastAsync();

        return response.Payload;
    }

    [HttpGet("ValidationException")]
    public WeatherForecast[] ValidationException()
        => throw new ValidationException("DrnTest");

    [HttpGet("UnauthorizedException")]
    public WeatherForecast[] UnauthorizedException()
        => throw new UnauthorizedException("DrnTest");

    [HttpGet("ForbiddenException")]
    public WeatherForecast[] ForbiddenException()
        => throw new ForbiddenException("DrnTest");

    [HttpGet("NotFoundException")]
    public WeatherForecast[] NotFoundException()
        => throw new NotFoundException("DrnTest");

    [HttpGet("ConflictException")]
    public WeatherForecast[] ConflictException()
        => throw new ConflictException("DrnTest");

    [HttpGet("ExpiredException")]
    public WeatherForecast[] ExpiredException()
        => throw new ExpiredException("DrnTest");

    [HttpGet("ConfigurationException")]
    public WeatherForecast[] ConfigurationException()
        => throw new ConfigurationException("DrnTest");

    [HttpGet("UnprocessableEntityException")]
    public WeatherForecast[] UnprocessableEntityException()
        => throw new UnprocessableEntityException("DrnTest");

    [HttpGet("MaliciousRequestException")]
    public WeatherForecast[] MaliciousRequestException()
        => throw new MaliciousRequestException("DrnTest");
}