using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;

namespace DRN.Framework.Hosting.Endpoints;

public interface IEndpointHelper
{
    EndpointDataSource EndpointDataSource { get; }
    LinkGenerator LinkGenerator { get; }
}

[Singleton<IEndpointHelper>]
public class EndpointHelper(EndpointDataSource endpointDataSource, LinkGenerator linkGenerator) : IEndpointHelper
{
    public EndpointDataSource EndpointDataSource { get; } = endpointDataSource;
    public LinkGenerator LinkGenerator { get; } = linkGenerator;
}