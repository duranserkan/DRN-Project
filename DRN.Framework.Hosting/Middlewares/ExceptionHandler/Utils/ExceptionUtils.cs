// This file is licensed to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;
using DRN.Framework.SharedKernel.Json;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils;


public interface IExceptionUtils
{
    DeveloperExceptionPageOptions ExceptionPageOptions { get; }
    ProblemDetails CreateProblemDetails(HttpContext context, Exception exception);
    Task<DrnExceptionModel> CreateErrorPageModelAsync(HttpContext context, Exception exception);
    CompilationErrorModel CreateCompilationErrorModel(HttpContext context, Exception exception, ICompilationException compilationException);
}

[Transient<IExceptionUtils>]
public class ExceptionUtils(
    ExceptionDetailsProvider exceptionDetailsProvider,
    IOptions<DeveloperExceptionPageOptions>? options = null)
    : IExceptionUtils
{
    private static readonly ExtensionsExceptionJsonContext SerializationContext = new(JsonConventions.DefaultOptions);

    public DeveloperExceptionPageOptions ExceptionPageOptions { get; } = options?.Value ?? new DeveloperExceptionPageOptions();

    public ProblemDetails CreateProblemDetails(HttpContext context, Exception exception)
    {
        var problemDetails = new ProblemDetails
        {
            Title = TypeNameHelper.GetTypeDisplayName(exception.GetType()),
            Detail = exception.Message,
            Status = context.Response.StatusCode
        };

        // Problem details source gen serialization doesn't know about IHeaderDictionary or RouteValueDictionary.
        // Serialize payload to a JsonElement here. Problem details serialization can write JsonElement in extensions dictionary.
        problemDetails.Extensions["exception"] = JsonSerializer.SerializeToElement(new ExceptionExtensionData
        (
            details: exception.ToString(),
            headers: context.Request.Headers,
            path: context.Request.Path.ToString(),
            endpoint: context.GetEndpoint()?.ToString(),
            routeValues: context.Features.Get<IRouteValuesFeature>()?.RouteValues
        ), SerializationContext.ExceptionExtensionData);

        return problemDetails;
    }

    public CompilationErrorModel CreateCompilationErrorModel(HttpContext context, Exception exception, ICompilationException compilationException)
    {
        var model = new CompilationErrorModel(ExceptionPageOptions);

        if (compilationException.CompilationFailures == null)
            return model;

        foreach (var compilationFailure in compilationException.CompilationFailures)
        {
            if (compilationFailure == null)
                continue;

            var stackFrames = new List<StackFrameSourceCodeInfo>();
            var exceptionDetails = new ExceptionDetails(compilationFailure.FailureSummary!, stackFrames);
            model.ErrorDetails.Add(exceptionDetails);
            model.CompiledContent.Add(compilationFailure.CompiledContent);

            if (compilationFailure.Messages == null)
                continue;

            var sourceLines = compilationFailure.SourceFileContent?.Split([Environment.NewLine], StringSplitOptions.None);
            foreach (var item in compilationFailure.Messages)
            {
                if (item == null)
                    continue;

                var frame = new StackFrameSourceCodeInfo
                {
                    File = compilationFailure.SourceFilePath,
                    Line = item.StartLine,
                    Function = string.Empty
                };

                if (sourceLines != null)
                    exceptionDetailsProvider.ReadFrameContent(frame, sourceLines, item.StartLine, item.EndLine);

                frame.ErrorDetails = item.Message;
                stackFrames.Add(frame);
            }
        }

        return model;
    }

    public async Task<DrnExceptionModel> CreateErrorPageModelAsync(HttpContext context, Exception exception)
    {
        var request = context.Request;
        var appSettings = context.RequestServices.GetRequiredService<IAppSettings>();
        var scopedLog = context.RequestServices.GetRequiredService<IScopedLog>();
        var title = GetPageTitle(exception);
        var body = await new StreamReader(request.Body).ReadToEndAsync();
        request.Body.Seek(0, SeekOrigin.Begin);

        var model = new DrnExceptionModel
        {
            Title = title,
            Options = ExceptionPageOptions,
            ErrorDetails = exceptionDetailsProvider.GetDetails(exception).ToArray(),
            Query = new QueryStringModel(request.Query, request.QueryString),
            Cookies = request.Cookies.ToDictionary(pair => pair.Key, pair => request.Cookies[pair.Key]),
            Headers = request.Headers.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()),
            RouteValues = request.RouteValues.ToDictionary(pair => pair.Key, pair => pair.Value),
            Endpoint = EndpointUtils.GetEndpointModel(context.GetEndpoint()),
            ConfigurationDebugViewSummary = appSettings.GetDebugView().ToSummary(),
            Logs = scopedLog.Logs,
            RequestMethod = request.Method,
            RequestPath = request.Path,
            RequestBody = body,
            RequestScheme = request.Scheme,
            RequestProtocol = request.Protocol
        };

        return model;
    }

    private static string GetPageTitle(Exception ex)
    {
        var title = "ErrorPage";
        if (ex is not BadHttpRequestException badHttpRequestException) return title;

        var badRequestReasonPhrase = Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(badHttpRequestException.StatusCode);
        if (!string.IsNullOrEmpty(badRequestReasonPhrase))
            title = badRequestReasonPhrase;

        return title;
    }
}

public sealed class ExceptionExtensionData(string details, IHeaderDictionary headers, string path, string? endpoint, RouteValueDictionary? routeValues)
{
    public string Details { get; } = details;
    public IHeaderDictionary Headers { get; } = headers;
    public string Path { get; } = path;
    public string? Endpoint { get; } = endpoint;
    public RouteValueDictionary? RouteValues { get; } = routeValues;
}

[JsonSerializable(typeof(ExceptionExtensionData))]
public sealed partial class ExtensionsExceptionJsonContext : JsonSerializerContext
{
}