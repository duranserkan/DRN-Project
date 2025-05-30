using DRN.Framework.Hosting.Endpoints;
using DRN.Nexus.Hosted.Controllers.Sample;

namespace DRN.Nexus.Hosted.Controllers;

public class NexusEndpointFor : EndpointCollectionBase<NexusProgram>
{
    public const string Prefix = "/Api";
    public const string ControllerRouteTemplate = $"{Prefix}/[controller]";

    public UserApiFor User { get; } = new();
    public StatusFor Status { get; } = new();
    public WeatherForecastFor WeatherForecast { get; } = new();
}

public class StatusFor()
    : ControllerForBase<StatusController>(NexusEndpointFor.ControllerRouteTemplate)
{
    //By convention Endpoint name should match Action name and property should have setter;
    public ApiEndpoint Status { get; private set; } = null!;
}

public class WeatherForecastFor()
    : ControllerForBase<WeatherForecastController>(NexusEndpointFor.ControllerRouteTemplate)
{
    //By convention Endpoint name should match Action name and property should have setter;
    public ApiEndpoint Get { get; private set; } = null!;
    public ApiEndpoint Private { get; private set; } = null!;
}