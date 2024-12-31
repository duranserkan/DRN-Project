// This file is licensed to you under the MIT license.

using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;

public class QueryStringModel
{
    [JsonConstructor]
    public QueryStringModel()
    {
    }
    
    public QueryStringModel(IQueryCollection queryCollection, QueryString queryString)
    {
        foreach (var key in queryCollection.Keys)
            Queries.Add(key, queryCollection[key].ToArray());

        QueryString = queryString.Value;
    }

    public Dictionary<string, string?[]> Queries { get; init; } = new();
    public string? QueryString { get; init; }
}