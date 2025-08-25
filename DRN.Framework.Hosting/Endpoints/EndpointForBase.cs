using System.Reflection;
using DRN.Framework.Hosting.Extensions;
using DRN.Framework.SharedKernel;
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

    private static ApiEndpoint CreateEndpoint(string actionMethodName) => new(Controller, actionMethodName);

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
    public ApiEndpoint(Type controller, string actionMethodName)
    {
        ActionMethodName = actionMethodName;
        ControllerName = controller.Name.Replace("Controller", string.Empty);
        ControllerClassName = controller.Name;
        EndpointName = $"{ControllerClassName}.{ActionMethodName}";
    }

    public string ActionMethodName { get; }
    public string ControllerName { get; }
    public string ControllerClassName { get; }
    public string EndpointName { get; }
    public string? RoutePattern { get; private set; }
    public string[]? RoutePatterns { get; private set; }
    public RouteEndpoint[] RouteEndpoints { get; private set; } = null!;
    public ControllerActionDescriptor[] ActionDescriptor { get; private set; } = null!;
    public string EndpointKey { get; private set; } = null!;


    public string Path() => RoutePattern ?? string.Empty;

    public string Path(Guid id, string template = "{id:guid}")
        => RoutePattern?.Replace("{id:guid}", id.ToString("N")) ?? string.Empty;

    public string Path(Dictionary<string, string> parameters)
    {
        if (string.IsNullOrEmpty(RoutePattern))
            return string.Empty;

        var path = RoutePattern;
        foreach (var kvp in parameters)
        {
            var placeholder = $"{{{kvp.Key}}}";
            if (path.Contains(placeholder))
            {
                path = path.Replace(placeholder, kvp.Value);
            }
        }

        return path;
    }

    internal void SetEndPoint(DrnEndpointSource source)
    {
        EndpointKey = this.GetEndpointKey();
        var routeEndpoint = source.EndpointMap[EndpointKey];
        RouteEndpoints = routeEndpoint ?? throw new ValidationException($"Endpoint not found for {EndpointKey}");
        RoutePatterns = RouteEndpoints.Select(route => route.RoutePattern.RawText!).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        RoutePattern = RoutePatterns.FirstOrDefault();
        ActionDescriptor = RouteEndpoints.SelectMany(route => route.Metadata.OfType<ControllerActionDescriptor>()).ToArray();
    }
}