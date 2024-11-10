using System.Reflection;
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Utils.Extensions;
using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.Endpoints;

public abstract class EndpointCollectionBase<TProgram> where TProgram : DrnProgramBase<TProgram>, IDrnProgram, new()
{
    private static bool _triggered;
    private static readonly SemaphoreSlim StartupLock = new(1, 1);
    private static IReadOnlyList<Endpoint> Endpoints { get; set; } = [];
    public static IReadOnlyList<ApiEndpoint> ApiEndpoints { get; private set; } = [];

    internal static void SetEndpointDataSource(IEndpointHelper endpointHelper)
    {
        if (_triggered) return;

        StartupLock.Wait();
        try
        {
            if (_triggered) return;
            Endpoints = endpointHelper.EndpointDataSource.Endpoints;
            ApiEndpoints = InitializeEndpoints();
            _triggered = true;
        }
        finally
        {
            StartupLock.Release();
        }
    }

    private static ApiEndpoint[] InitializeEndpoints()
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