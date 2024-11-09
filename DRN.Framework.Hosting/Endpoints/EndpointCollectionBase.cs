using System.Reflection;
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Utils.Extensions;
using Microsoft.AspNetCore.Routing;

namespace DRN.Framework.Hosting.Endpoints;

public abstract class EndpointCollectionBase<TProgram> where TProgram : DrnProgramBase<TProgram>, IDrnProgram, new()
{
    private static readonly Lazy<ApiEndpoint[]> AllEndpoints = new(InitializeEndpoints);
    public static ApiEndpoint[] GetAllEndpoints() => AllEndpoints.Value;

    public static IEndpointHelper EndpointHelper { get; private set; } = null!;
    public static EndpointDataSource DataSource { get; private set; } = null!;

    internal static void SetEndpointDataSource(IEndpointHelper endpointHelper)
    {
        //todo: trigger initialize endpoints and cache endpoint data once it is triggered
        //todo: clear endpoint helper reference since it may contain disposable instances
        //todo: can keep list of all endpoints
        EndpointHelper = endpointHelper;
        DataSource = endpointHelper.EndpointDataSource;
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
        var endpointBase = typeof(IEndpointForBase);
        foreach (var apiGroup in apiGroups.Values)
        {
            if (endpointBase.IsInstanceOfType(apiGroup))
            {
                var apiForBase = (IEndpointForBase)apiGroup;
                SetEndpoint(apiForBase, endpointList);
            }

            var endPointContainers = apiGroup.GetGroupedPropertiesOfSubtype(endpointBase);
            foreach (var container in endPointContainers)
            {
                var containerObject = container.Key;
                foreach (var propertyInfo in container.Value)
                {
                    var apiForBase = (IEndpointForBase?)propertyInfo.GetValue(containerObject);
                    SetEndpoint(apiForBase, endpointList);
                }
            }
        }

        return endpointList
            .OrderBy(x => x.ControllerClassName)
            .ThenBy(x => x.ActionName).ToArray();
    }

    private static void SetEndpoint(IEndpointForBase? apiForBase, HashSet<ApiEndpoint> endpointList)
    {
        if (apiForBase == null) return;

        foreach (var page in apiForBase.Endpoints)
        {
            page.SetEndPoint(EndpointHelper);
            endpointList.Add(page);
        }
    }
}