using DRN.Framework.Hosting.Endpoints;

namespace DRN.Nexus.Hosted.Controllers;

public abstract class EndpointFor : EndpointCollectionBase<Program>
{
    public const string Prefix = "/Api";
    public const string ControllerRouteTemplate = $"{Prefix}/[controller]";

    public static StatusFor Status { get; } = new();
    public static WeatherForecastFor WeatherForecast { get; } = new();
}

public class StatusFor()
    : ControllerForBase<StatusController>(EndpointFor.ControllerRouteTemplate)
{
    //By convention Endpoint name should match Action name and property should have setter;
    public ApiEndpoint Status { get; private set; } = null!;
}

public class WeatherForecastFor()
    : ControllerForBase<WeatherForecastController>(EndpointFor.ControllerRouteTemplate)
{
    //By convention Endpoint name should match Action name and property should have setter;
    public ApiEndpoint Get { get; private set; } = null!;
    public ApiEndpoint Private { get; private set; } = null!;
}