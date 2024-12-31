// This file is licensed to you under the MIT license.

using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;

public class EndpointMetadataCollectionModel
{
    [JsonConstructor]
    public EndpointMetadataCollectionModel()
    {
    }

    public EndpointMetadataCollectionModel(EndpointMetadataCollection metadataCollection)
    {
        Values = metadataCollection.Select(metadata => new MetadataModel(metadata)).ToArray();
    }

    public MetadataModel[] Values { get; init; } = [];
}

public class MetadataModel
{
    [JsonConstructor]
    private MetadataModel(string name, string value)
    {
        Name = name;
        Value = value;
    }

    public MetadataModel(object metadata)
    {
        Name = metadata.GetType().FullName ?? metadata.GetType().Name;
        Value = metadata.ToString() ?? string.Empty;
    }

    public string Name { get; init; }
    public string Value { get; init; }
};