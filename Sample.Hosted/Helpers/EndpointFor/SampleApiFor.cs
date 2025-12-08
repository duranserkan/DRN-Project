using DRN.Framework.Hosting.Endpoints;
using Sample.Hosted.Controllers.Sample;

namespace Sample.Hosted.Helpers.EndpointFor;
//todo evaluate source generator base implementation
public class SampleApiFor
{
    public const string Prefix = "/Api/Sample";
    public const string ControllerRouteTemplate = $"{Prefix}/[controller]";

    public PrivateFor Private { get; } = new();
    public WeatherForecastFor WeatherForecast { get; } = new();
    public ExceptionControllerFor Exception { get; } = new();
}

public class PrivateFor()
    : ControllerForBase<PrivateController>(SampleApiFor.ControllerRouteTemplate)
{
    //By convention, Endpoint name should match Action name and property should have setter;
    public ApiEndpoint Authorized { get; private set; } = null!;
    public ApiEndpoint Anonymous { get; private set; } = null!;
    public ApiEndpoint Context { get; private set; } = null!;
    public ApiEndpoint ValidateScope { get; private set; } = null!;
}

public class WeatherForecastFor()
    : ControllerForBase<WeatherForecastController>(SampleApiFor.ControllerRouteTemplate)
{
    //By convention, Endpoint name should match Action name and property should have setter;
    public ApiEndpoint Get { get; private set; } = null!;
    public ApiEndpoint GetNexusWeatherForecasts { get; private set; } = null!;
}

public class ExceptionControllerFor()
    : ControllerForBase<ExceptionController>(SampleApiFor.ControllerRouteTemplate)
{
    //By convention, Endpoint name should match Action name and property should have setter;
    public ApiEndpoint ValidationException { get; private set; } = null!;
    public ApiEndpoint UnauthorizedException { get; private set; } = null!;
    public ApiEndpoint ForbiddenException { get; private set; } = null!;
    public ApiEndpoint NotFoundException { get; private set; } = null!;
    public ApiEndpoint ConflictException { get; private set; } = null!;
    public ApiEndpoint ExpiredException { get; private set; } = null!;
    public ApiEndpoint ConfigurationException { get; private set; } = null!;
    public ApiEndpoint UnprocessableEntityException { get; private set; } = null!;
    public ApiEndpoint MaliciousRequestException { get; private set; } = null!;
    
    public ApiEndpoint GetErrorPageModel { get; private set; } = null!;
}