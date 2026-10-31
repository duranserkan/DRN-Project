// This file is licensed to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;
using DRN.Framework.SharedKernel.Json;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Logging.Diagnostics;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
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
    IAppSettings appSettings,
    IOptions<DeveloperExceptionPageOptions>? options = null)
    : IExceptionUtils
{
    private static readonly ExtensionsExceptionJsonContext SerializationContext = new(JsonConventions.DefaultOptions);

    public DeveloperExceptionPageOptions ExceptionPageOptions { get; } = options?.Value ?? new DeveloperExceptionPageOptions();

    public ProblemDetails CreateProblemDetails(HttpContext context, Exception exception)
    {
        var includeExceptionDetails = appSettings.IsDevelopmentEnvironment;
        var problemDetails = new ProblemDetails
        {
            Title = includeExceptionDetails
                ? TypeNameHelper.GetTypeDisplayName(exception.GetType())
                : "An error occurred while processing the request.",
            Status = context.Response.StatusCode
        };

        if (!includeExceptionDetails)
            return problemDetails;

        var sanitizer = Sanitizer(context);
        var projection = new DiagnosticModelProjection(sanitizer);
        problemDetails.Detail = projection.Text(exception.Message);

        // Problem details source gen serialization doesn't know about IHeaderDictionary or RouteValueDictionary.
        // Serialize payload to a JsonElement here. Problem details serialization can write JsonElement in extensions dictionary.
        problemDetails.Extensions["exception"] = JsonSerializer.SerializeToElement(new ExceptionExtensionData
        (
            details: projection.Format(exception),
            headers: new HeaderDictionary(projection.Strings(context.Request.Headers.Select(pair =>
                    new KeyValuePair<string, string?[]>(pair.Key, pair.Value.Take(sanitizer.Options.MaxCollectionEntries).ToArray())))
                .ToDictionary(pair => pair.Key, pair => new Microsoft.Extensions.Primitives.StringValues(pair.Value))),
            path: sanitizer.SanitizeUrl(context.Request.Path.ToString()),
            endpoint: projection.Text(context.GetEndpoint()?.DisplayName),
            routeValues: new RouteValueDictionary(SafeRoutes(context, sanitizer))
        ), SerializationContext.ExceptionExtensionData);

        return problemDetails;
    }

    public CompilationErrorModel CreateCompilationErrorModel(HttpContext context, Exception exception, ICompilationException compilationException)
    {
        if (!appSettings.IsDevelopmentEnvironment)
            throw new InvalidOperationException("Developer compilation exception page models are only available in Development.");

        var model = new CompilationErrorModel(ExceptionPageOptions);
        var sanitizer = Sanitizer(context, forDevelopmentDisplay: true);
        var projection = new DiagnosticModelProjection(sanitizer);
        if (compilationException.CompilationFailures == null)
            return model;

        var remainingFrames = sanitizer.Options.MaxCollectionEntries;
        foreach (var compilationFailure in compilationException.CompilationFailures.Take(sanitizer.Options.MaxCollectionEntries))
        {
            if (remainingFrames <= 0)
            {
                model.ErrorDetails.Add(new ExceptionDetails(DiagnosticSanitizer.Limited, []));
                model.CompiledContent.Add(null);
                break;
            }
            if (compilationFailure == null)
                continue;

            var stackFrames = new List<StackFrameSourceCodeInfo>();
            var exceptionDetails = new ExceptionDetails(projection.Text(compilationFailure.FailureSummary), stackFrames);
            model.ErrorDetails.Add(exceptionDetails);
            model.CompiledContent.Add(projection.Text(compilationFailure.CompiledContent));

            if (compilationFailure.Messages == null)
                continue;

            var sourceLines = projection.Text(compilationFailure.SourceFileContent).Split([Environment.NewLine], StringSplitOptions.None);
            foreach (var item in compilationFailure.Messages.Take(remainingFrames))
            {
                if (item == null)
                    continue;

                var frame = new StackFrameSourceCodeInfo
                {
                    File = projection.Text(compilationFailure.SourceFilePath),
                    Line = item.StartLine,
                    Function = string.Empty
                };

                exceptionDetailsProvider.ReadFrameContent(frame, sourceLines, item.StartLine, item.EndLine);

                frame.ErrorDetails = projection.Text(item.Message);
                stackFrames.Add(frame);
                remainingFrames--;
            }
        }

        return model;
    }

    public async Task<DrnExceptionModel> CreateErrorPageModelAsync(HttpContext context, Exception exception)
    {
        var request = context.Request;
        var scopedLog = context.RequestServices.GetRequiredService<IScopedLog>();
        var sanitizer = Sanitizer(context, forDevelopmentDisplay: true);
        var projection = new DiagnosticModelProjection(sanitizer);
        var title = GetPageTitle(exception);
        var body = await RequestBufferingState.ReadBodyAsync(context, context.RequestAborted);
        var safeUrl = sanitizer.SanitizeUrl(request.PathBase.Add(request.Path).Value + request.QueryString.Value);
        var queryStart = safeUrl.IndexOf('?');
        var safeQuery = queryStart < 0 ? string.Empty : safeUrl[queryStart..];

        var model = new DrnExceptionModel
        {
            Title = title,
            Options = ExceptionPageOptions,
            ErrorDetails = exceptionDetailsProvider.GetDetails(exception).Take(sanitizer.Options.MaxCollectionEntries).Select(projection.Exception).ToArray(),
            Query = new QueryStringModel { QueryString = safeQuery, Queries = QueryHelpers.ParseQuery(safeQuery).ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()) },
            Cookies = projection.Strings(request.Cookies.Select(pair => new KeyValuePair<string, string?[]>(pair.Key, [pair.Value])), true)
                .ToDictionary(pair => pair.Key, pair => pair.Value.FirstOrDefault()),
            Headers = projection.Strings(request.Headers.Select(pair => new KeyValuePair<string, string?[]>(pair.Key, pair.Value.Take(sanitizer.Options.MaxCollectionEntries).ToArray()))),
            RouteValues = SafeRoutes(context, sanitizer),
            Endpoint = projection.Endpoint(context.GetEndpoint()),
            ConfigurationDebugViewSummary = appSettings.GetDebugView().ToSummary(),
            Logs = scopedLog is ScopedLog native ? native.GetLogs(redact: !appSettings.IsDevelopmentEnvironment)
                : sanitizer.SanitizeValue("", scopedLog.GetLogs()) as IReadOnlyDictionary<string, object>
                ?? new Dictionary<string, object> { ["DiagnosticCapture"] = DiagnosticSanitizer.Limited },
            RequestMethod = projection.Text(request.Method),
            RequestPath = queryStart < 0 ? safeUrl : safeUrl[..queryStart],
            RequestBody = sanitizer.SanitizeBody(body, request.ContentType, request.Path.ToString()),
            RequestScheme = projection.Text(request.Scheme),
            RequestProtocol = projection.Text(request.Protocol)
        };

        return model;
    }

    private IDiagnosticSanitizer Sanitizer(HttpContext context, bool forDevelopmentDisplay = false)
    {
        var sanitizer = (context.RequestServices.GetService<IScopedLog>() as ScopedLog)?.Sanitizer ?? DiagnosticSanitizer.FromSettings(appSettings);
        return forDevelopmentDisplay && appSettings.IsDevelopmentEnvironment && sanitizer is DiagnosticSanitizer
            ? DiagnosticSanitizer.ForDevelopmentDisplay(appSettings, sanitizer.Options) : sanitizer;
    }

    private static Dictionary<string, object?> SafeRoutes(HttpContext context, IDiagnosticSanitizer sanitizer) =>
        sanitizer.SanitizeValue(context.Request.Path + ".RouteValues", context.Request.RouteValues) as Dictionary<string, object?> ??
        new Dictionary<string, object?> { ["DiagnosticCapture"] = DiagnosticSanitizer.Limited };

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
