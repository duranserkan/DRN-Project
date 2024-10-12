using DRN.Framework.Utils.Models;

namespace Sample.Hosted.Controllers;

[AllowAnonymous]
[ApiController]
[Route("[controller]")]
public class ExceptionController
{
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