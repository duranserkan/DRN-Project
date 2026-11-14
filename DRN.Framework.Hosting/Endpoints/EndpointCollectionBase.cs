using System.Diagnostics.CodeAnalysis;
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Hosting.Extensions;
using DRN.Framework.Utils.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;

namespace DRN.Framework.Hosting.Endpoints;

[SuppressMessage("SonarQube", "S2743", Justification = "Each hosted program initializes its own endpoint catalog under its own lock.")]
[SuppressMessage("ReSharper", "StaticMemberInGenericType")]
internal static class EndpointCatalog<TProgram>
    where TProgram : DrnProgramBase<TProgram>, IDrnProgram, new()
{
    private static bool _triggered;
    private static readonly SemaphoreSlim StartupLock = new(1, 1);

    public static DrnEndpointSource EndpointSource { get; private set; } = new();
    public static IReadOnlyList<Endpoint> Endpoints { get; private set; } = [];
    public static IReadOnlyList<PageEndpoint> PageEndpoints { get; private set; } = [];
    public static IReadOnlyList<ApiEndpoint> ApiEndpoints { get; private set; } = [];

    /// <summary>
    /// Assuming that all instances of the program will have the same endpoints so that we can initialize this once.
    /// There may be exceptions, but we consider it a bad practice and don't support it.
    /// </summary>
    [SuppressMessage("SonarQube", "S2589", Justification = "Another caller can publish the endpoint catalog while this caller waits for StartupLock.")]
    internal static void SetEndpointDataSource(IEndpointHelper endpointHelper, EndpointCollection<TProgram>? collection)
    {
        if (_triggered) return;

        StartupLock.Wait();
        try
        {
            if (_triggered) return;

            Endpoints = endpointHelper.EndpointDataSource.Endpoints;
            EndpointSource = new DrnEndpointSource(endpointHelper.EndpointDataSource);

            PageEndpoints = InitializePageEndpoints();
            ApiEndpoints = collection?.Bind(EndpointSource) ?? [];
            _triggered = true;
        }
        finally
        {
            StartupLock.Release();
        }
    }

    private static PageEndpoint[] InitializePageEndpoints()
    {
        var pageEndpoints = Endpoints
            .Where(e => e is RouteEndpoint { RoutePattern.RawText: not null }
                        && e.Metadata.GetMetadata<PageActionDescriptor>() is not null)
            .Cast<RouteEndpoint>()
            .GroupBy(e => e.Metadata.GetMetadata<PageActionDescriptor>()!.RelativePath)
            .Select(g => new PageEndpoint(g.ToArray()));

        return pageEndpoints.ToArray();
    }
}

/// <summary>A program-owned collection of controller accessors supplied to host initialization.</summary>
public abstract class EndpointCollection<TProgram>
    where TProgram : DrnProgramBase<TProgram>, IDrnProgram, new()
{
    internal ApiEndpoint[] Bind(DrnEndpointSource source)
    {
        var collectionType = GetType();
        var apiGroups = collectionType.GetProperties()
            // Do not traverse the singleton itself. Other static groups remain discoverable.
            .Where(property => !(property is { Name: "EndpointCollection", GetMethod.IsStatic: true } &&
                                 property.PropertyType == collectionType))
            .Where(p => p.GetValue(this) != null)
            .ToDictionary(property => property, property => property.GetValue(this)!);

        HashSet<ApiEndpoint> endpointList = [];
        var endpointBase = typeof(IApiEndpointForBase);
        foreach (var apiGroup in apiGroups.Values)
        {
            if (endpointBase.IsInstanceOfType(apiGroup))
            {
                var apiForBase = (IApiEndpointForBase)apiGroup;
                SetEndpoint(apiForBase, endpointList, source);
            }

            var endPointContainers = apiGroup.GetGroupedPropertiesOfSubtype(endpointBase);
            foreach (var container in endPointContainers)
            {
                var containerObject = container.Key;
                foreach (var propertyInfo in container.Value)
                {
                    var apiForBase = (IApiEndpointForBase?)propertyInfo.GetValue(containerObject);
                    SetEndpoint(apiForBase, endpointList, source);
                }
            }
        }

        return endpointList
            .OrderBy(x => x.ControllerClassName, StringComparer.Ordinal)
            .ThenBy(x => x.ActionMethodName, StringComparer.Ordinal).ToArray();
    }

    private static void SetEndpoint(IApiEndpointForBase? apiForBase, HashSet<ApiEndpoint> endpointList, DrnEndpointSource source)
    {
        if (apiForBase == null) return;

        foreach (var apiEndpoint in apiForBase.Endpoints)
        {
            apiEndpoint.SetEndPoint(source);
            endpointList.Add(apiEndpoint);
        }
    }
}

/// <summary>Creates one strongly typed endpoint collection for its program and collection type.</summary>
[SuppressMessage("ReSharper", "StaticMemberInGenericType")]
[SuppressMessage("SonarQube", "S2743", Justification = "Each collection type owns its typed singleton.")]
public abstract class EndpointCollectionBase<TProgram, TCollection> : EndpointCollection<TProgram>
    where TProgram : DrnProgramBase<TProgram>, IDrnProgram, new()
    where TCollection : EndpointCollectionBase<TProgram, TCollection>, new()
{
    public static TCollection EndpointCollection { get; } = new();
}

public class DrnEndpointSource
{
    public DrnEndpointSource(EndpointDataSource endpointDataSource)
    {
        EndpointDataSource = endpointDataSource;
        Endpoints = endpointDataSource.Endpoints;
        EndpointMap = Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() != null)
            .GroupBy(endpoint => endpoint.Metadata.GetRequiredMetadata<ControllerActionDescriptor>().GetEndpointKey())
            .ToDictionary(
                group => group.Key,
                group => group.ToArray());

        DescriptorMap = Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() != null)
            .GroupBy(endpoint => endpoint.Metadata.GetRequiredMetadata<ControllerActionDescriptor>().GetEndpointKey())
            .ToDictionary(
                group => group.Key,
                group => group.Select(endpoint => endpoint.Metadata.GetRequiredMetadata<ControllerActionDescriptor>()).ToArray());

        EndpointDescriptorMap = Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() != null)
            // An action descriptor can be shared by multiple route selectors.
            // Keep the legacy single-route view while EndpointMap retains every selector.
            .GroupBy(endpoint => endpoint.Metadata.GetRequiredMetadata<ControllerActionDescriptor>())
            .ToDictionary(
                group => group.Key,
                group => group.First());
    }

    public DrnEndpointSource()
    {
    }

    public EndpointDataSource EndpointDataSource { get; set; } = null!;
    public IReadOnlyList<Endpoint> Endpoints { get; } = [];

    public Dictionary<string, RouteEndpoint[]> EndpointMap { get; } = new();
    public Dictionary<string, ControllerActionDescriptor[]> DescriptorMap { get; } = new();
    public Dictionary<ControllerActionDescriptor, RouteEndpoint> EndpointDescriptorMap { get; } = new();
}
