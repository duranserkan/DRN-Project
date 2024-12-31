using System.Reflection;
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Utils.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;

namespace DRN.Framework.Hosting.Endpoints;

public abstract class EndpointCollectionBase<TProgram> where TProgram : DrnProgramBase<TProgram>, IDrnProgram, new()
{
    private static bool _triggered;
    private static readonly SemaphoreSlim StartupLock = new(1, 1);
    public static IReadOnlyList<Endpoint> Endpoints { get; private set; } = [];
    public static IReadOnlyList<PageEndpoint> PageEndpoints { get; private set; } = [];
    public static IReadOnlyList<ApiEndpoint> ApiEndpoints { get; private set; } = [];

    /// <summary>
    /// Assuming that all instances of the program will have same endpoints so that we can initialize this once.
    /// There may be exceptions, but we consider it a bad practice and don't support it.
    /// </summary>
    internal static void SetEndpointDataSource(IEndpointHelper endpointHelper)
    {
        if (_triggered) return;

        StartupLock.Wait();
        try
        {
            if (_triggered) return;
            Endpoints = endpointHelper.EndpointDataSource.Endpoints;
            PageEndpoints = InitializePageEndpoints();
            ApiEndpoints = InitializeApiEndpoints();
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
            .Where(e => e is RouteEndpoint && e.Metadata.GetMetadata<PageActionDescriptor>() != null)
            .Select(e => new PageEndpoint((RouteEndpoint)e));

        return pageEndpoints.ToArray();
    }

    private static ApiEndpoint[] InitializeApiEndpoints()
    {
        var collectionBaseType = typeof(EndpointCollectionBase<TProgram>);
        var collectionType = typeof(TProgram).Assembly.GetSubTypes(collectionBaseType).FirstOrDefault();
        if (collectionType == null) return [];

        var apiGroups = collectionType
            .GetProperties(BindingFlags.Static | BindingFlags.Public)
            .Where(p => p.GetValue(null) != null)
            .ToDictionary(property => property, property => property.GetValue(null)!);

        HashSet<ApiEndpoint> endpointList = [];
        var endpointBase = typeof(IApiEndpointForBase);
        foreach (var apiGroup in apiGroups.Values)
        {
            if (endpointBase.IsInstanceOfType(apiGroup))
            {
                var apiForBase = (IApiEndpointForBase)apiGroup;
                SetEndpoint(apiForBase, endpointList);
            }

            var endPointContainers = apiGroup.GetGroupedPropertiesOfSubtype(endpointBase);
            foreach (var container in endPointContainers)
            {
                var containerObject = container.Key;
                foreach (var propertyInfo in container.Value)
                {
                    var apiForBase = (IApiEndpointForBase?)propertyInfo.GetValue(containerObject);
                    SetEndpoint(apiForBase, endpointList);
                }
            }
        }

        return endpointList
            .OrderBy(x => x.ControllerClassName)
            .ThenBy(x => x.ActionName).ToArray();
    }

    private static void SetEndpoint(IApiEndpointForBase? apiForBase, HashSet<ApiEndpoint> endpointList)
    {
        if (apiForBase == null) return;

        foreach (var apiEndpoint in apiForBase.Endpoints)
        {
            apiEndpoint.SetEndPoint(Endpoints);
            endpointList.Add(apiEndpoint);
        }
    }
}