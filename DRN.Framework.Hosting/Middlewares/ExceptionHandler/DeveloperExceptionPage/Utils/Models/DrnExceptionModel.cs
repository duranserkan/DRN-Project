// This file is licensed to you under the MIT license.

using System.Text.Json.Serialization;
using DRN.Framework.Utils.Configurations;
using Microsoft.AspNetCore.Builder;

namespace DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;

/// <summary>
/// Holds data to be displayed on the error page.
/// </summary>
public class DrnExceptionModel
{
    /// <summary>
    /// The text be inside the HTML title element.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Options for what output to display.
    /// </summary>
    [JsonIgnore]
    public DeveloperExceptionPageOptions Options { get; set; } = new();

    /// <summary>
    /// Detailed information about each exception in the stack.
    /// </summary>
    public IReadOnlyList<ExceptionDetails> ErrorDetails { get; set; } = [];

    /// <summary>
    /// Parsed query data.
    /// </summary>
    public QueryStringModel Query { get; set; } = new();

    /// <summary>
    /// Request cookies.
    /// </summary>
    public Dictionary<string, string?> Cookies { get; init; } = new();

    /// <summary>
    /// Request headers.
    /// </summary>
    public Dictionary<string, string?[]> Headers { get; init; } = new();

    /// <summary>
    /// Request route values.
    /// </summary>
    public Dictionary<string, object?> RouteValues { get; set; } = new();

    /// <summary>
    /// Request endpoint.
    /// </summary>
    public EndpointModel? Endpoint { get; set; }

    public ConfigurationDebugViewSummary ConfigurationDebugViewSummary { get; init; } = null!;
    public IReadOnlyDictionary<string, object> Logs { get; init; } = new Dictionary<string, object>();
    public string RequestPath { get; set; } = string.Empty;
    public string RequestMethod { get; set; } = string.Empty;
    public string RequestScheme { get; set; } = string.Empty;
    public string RequestProtocol { get; set; } = string.Empty;
    public string RequestBody { get; set; } = string.Empty;
}