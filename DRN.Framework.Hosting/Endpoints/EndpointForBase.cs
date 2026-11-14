using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.Hosting.Extensions;
using DRN.Framework.SharedKernel;
using DRN.Framework.Utils.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;

namespace DRN.Framework.Hosting.Endpoints;

public interface IApiEndpointForBase
{
    ApiEndpoint[] Endpoints { get; }
}

[SuppressMessage("ReSharper", "StaticMemberInGenericType")]
[SuppressMessage("SonarQube", "S2743", Justification = "Controller metadata is cached separately for each TController.")]
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
        .GetPropertiesOfType<ApiEndpoint>(BindingFlag.InstancePublic)
        .Select(p =>
        {
            //Endpoint name should match with Action name and property name
            var binding = p.GetCustomAttribute<EndpointActionAttribute>();
            var endpoint = binding == null
                ? CreateEndpoint(p.Name)
                : new ApiEndpoint(Controller, binding.MethodName, binding.ParameterTypes);
            p.SetValue(this, endpoint);

            return endpoint;
        }).ToArray();
}

public class ApiEndpoint
{
    private readonly Type? _bindingController;
    private readonly Type[]? _parameterTypes;

    /// <summary>Binds a generated or explicitly customized member to a concrete controller and CLR signature.</summary>
    public ApiEndpoint(Type controller, string actionMethodName, Type[] parameterTypes) : this(controller, actionMethodName)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(parameterTypes);
        _bindingController = controller;
        _parameterTypes = (Type[])parameterTypes.Clone();
    }

    public ApiEndpoint(Type controller, string actionMethodName)
    {
        ActionMethodName = actionMethodName;
        ControllerName = controller.Name.Replace("Controller", string.Empty);
        ControllerClassName = controller.Name;
        EndpointName = $"{ControllerClassName}.{ActionMethodName}";
    }

    public string ActionMethodName { get; }
    public string ControllerName { get; }
    [Sensitive(false, Justification = "Controller type name.")]
    public string ControllerClassName { get; }
    public string EndpointName { get; }
    public string? RoutePattern { get; private set; }
    public string[]? RoutePatterns { get; private set; }
    public RouteEndpoint[] RouteEndpoints { get; private set; } = null!;
    public ControllerActionDescriptor[] ActionDescriptor { get; private set; } = null!;
    internal bool HasAmbiguousActionAddress { get; private set; }
    [Sensitive(false, Justification = "Endpoint lookup identifier.")]
    public string EndpointKey { get; private set; } = null!;


    public string Path() => RoutePattern ?? string.Empty;

    public string Path(Guid id, string template = "{id:guid}")
        => RoutePattern?.Replace(template, id.ToString("N")) ?? string.Empty;

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
        RouteEndpoint[]? routeEndpoints;
        if (_bindingController == null)
            source.EndpointMap.TryGetValue(EndpointKey, out routeEndpoints);
        else
            routeEndpoints = source.Endpoints.OfType<RouteEndpoint>().Where(route =>
            {
                var action = route.Metadata.GetMetadata<ControllerActionDescriptor>();
                return action?.ControllerTypeInfo.AsType() == _bindingController &&
                       action.MethodInfo.Name == ActionMethodName &&
                       action.MethodInfo.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(_parameterTypes!);
            }).ToArray();
        if (routeEndpoints is not { Length: > 0 })
            throw new ValidationException($"Endpoint '{EndpointKey}' was not discovered. Check the controller's application part, action name and NonAction exclusions.");
        RouteEndpoints = routeEndpoints;
        RoutePatterns = RouteEndpoints.Select(route => route.RoutePattern.RawText!).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        RoutePattern = RoutePatterns.FirstOrDefault();
        ActionDescriptor = RouteEndpoints.SelectMany(route => route.Metadata.OfType<ControllerActionDescriptor>()).ToArray();
        HasAmbiguousActionAddress = EndpointActionMetadata.HasAmbiguousAddress(ActionDescriptor, source.Endpoints.OfType<RouteEndpoint>());
    }
}
