using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.Endpoints;

public interface IEndpointAccessor
{
    IReadOnlyList<Endpoint> Endpoints { get; }
    IReadOnlyList<ApiEndpoint> ApiEndpoints { get; }
    IReadOnlyList<PageEndpoint> PageEndpoints { get; }
    IReadOnlyDictionary<string, PageEndpoint> PageEndpointByPaths { get; }
    IEndpointHelper EndpointHelper { get; }
    Type ProgramType { get; }
}

public class EndpointAccessor(
    IEndpointHelper endpointHelper,
    IReadOnlyList<Endpoint> endpoints,
    IReadOnlyList<ApiEndpoint> apiEndpoints,
    IReadOnlyList<PageEndpoint> pageEndpoints,
    Type programType) : IEndpointAccessor
{
    public IReadOnlyList<Endpoint> Endpoints { get; } = endpoints;
    public IReadOnlyList<ApiEndpoint> ApiEndpoints { get; } = apiEndpoints;
    public IReadOnlyList<PageEndpoint> PageEndpoints { get; } = pageEndpoints;
    
    public IReadOnlyDictionary<string, PageEndpoint> PageEndpointByPaths { get; } = pageEndpoints.ToDictionary(p => p.ActionDescriptor.RelativePath, p => p);
    public IEndpointHelper EndpointHelper { get; } = endpointHelper;
    public Type ProgramType { get; } = programType;
}