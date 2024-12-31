// This file is licensed to you under the MIT license.

namespace DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;

public class EndpointModel
{
    public string? DisplayName { get; set; }
    public string? RoutePattern { get; set; }
    public int? Order { get; set; }
    public string? HttpMethods { get; set; }

    public EndpointMetadataCollectionModel MetadataCollection { get; init; } = new();
}