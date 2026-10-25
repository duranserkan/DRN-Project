// This file is licensed to you under the MIT license.

using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils;

public static class EndpointUtils
{
    public static EndpointModel? GetEndpointModel(Endpoint? endpoint)
    {
        if (endpoint == null)
            return null;

        var endpointModel = new EndpointModel
        {
            DisplayName = endpoint.DisplayName,
            MetadataCollection = new EndpointMetadataCollectionModel(endpoint.Metadata)
        };


        if (endpoint is not RouteEndpoint routeEndpoint)
            return endpointModel;

        endpointModel.RoutePattern = routeEndpoint.RoutePattern.RawText;
        endpointModel.Order = routeEndpoint.Order;

        var httpMethods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;
        if (httpMethods != null)
            endpointModel.HttpMethods = string.Join(", ", httpMethods);

        return endpointModel;
    }
}