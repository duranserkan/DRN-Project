using System.Reflection;
using DRN.Framework.Hosting.Extensions;
using DRN.Framework.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;

namespace DRN.Framework.Hosting.Endpoints;

public interface IApiEndpointForBase
{
    ApiEndpoint[] Endpoints { get; }
}

public abstract class ControllerForBase<TController>
    : IApiEndpointForBase where TController : ControllerBase
{
    public static readonly Type Controller = typeof(TController);
    public static readonly string ControllerName = Controller.Name.Replace("Controller", string.Empty);
    public static readonly string ControllerClassName = Controller.Name;

    protected ControllerForBase(string controllerRoute)
    {
        ControllerRoute = controllerRoute.Replace("[controller]", ControllerName);
        Endpoints = InitializeEndpoints();
    }

    public ApiEndpoint[] Endpoints { get; }
    public string ControllerRoute { get; }

    private static ApiEndpoint CreateEndpoint(string action) => new(Controller, action);

    private ApiEndpoint[] InitializeEndpoints() => GetType()
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(p => p.PropertyType == typeof(ApiEndpoint))
        .Select(p =>
        {
            //Endpoint name should match with Action name and property name
            var endpoint = CreateEndpoint(p.Name);
            p.SetValue(this, endpoint);

            return endpoint;
        }).ToArray();
}

public class ApiEndpoint
{
    public ApiEndpoint(Type controller, string actionName)
    {
        ActionName = actionName;
        ControllerName = controller.Name.Replace("Controller", string.Empty);
        ControllerClassName = controller.Name;
        EndpointName = $"{ControllerClassName}.{ActionName}";
    }

    public string ActionName { get; }
    public string ControllerName { get; }
    public string ControllerClassName { get; }
    public string EndpointName { get; }
    public string? RoutePattern { get; private set; }
    public RouteEndpoint RouteEndpoint { get; private set; } = null!;
    public ControllerActionDescriptor ActionDescriptor { get; private set; } = null!;

    internal void SetEndPoint(IReadOnlyList<Endpoint> endpoints)
    {
        var routeEndpoint = endpoints.GetEndpoint(ControllerClassName, ActionName);
        RouteEndpoint = routeEndpoint ?? throw new ValidationException($"Endpoint not found for {ControllerClassName}.{ActionName}");
        RoutePattern = RouteEndpoint.RoutePattern.RawText!;
        ActionDescriptor = RouteEndpoint.Metadata.OfType<ControllerActionDescriptor>().First();
    }
}