using System.Text.Json;
using DRN.Framework.SharedKernel.Json;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace DRN.Framework.Hosting.Middlewares.ApiErrors;

[Singleton<DrnApiErrorResponse>]
public sealed class DrnApiErrorResponse(IOptions<DrnApiErrorOptions> options)
{
    public bool IsApiRequest(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        // Method matching creates a non-route rejection endpoint with no application metadata.
        var methodRejected = endpoint is not null and not RouteEndpoint && endpoint.Metadata.Count == 0 &&
                             context.Response.StatusCode == StatusCodes.Status405MethodNotAllowed &&
                             !string.IsNullOrEmpty(context.Response.Headers.Allow.ToString());
        return endpoint != null && !methodRejected
            ? DrnApiEndpointMetadata.IsApi(endpoint.Metadata)
            : options.Value.PathPrefixes.Any(prefix => context.Request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsSupportedStatus(int status) => status is >= 400 and <= 599;

    internal bool CanHandle(HttpContext context, int? status = null) =>
        (context.GetEndpoint() is not { } endpoint || !DrnApiEndpointMetadata.IsDisabled(endpoint.Metadata)) &&
        IsApiRequest(context) && IsSupportedStatus(status ?? context.Response.StatusCode);

    internal async ValueTask<bool> TryWriteAsync(HttpContext context)
    {
        if (!CanHandle(context))
            return false;

        await WriteAsync(context);
        return true;
    }

    public static Task WriteAsync(HttpContext context, ProblemDetails? problem = null)
    {
        if (context.Response.HasStarted || context.RequestAborted.IsCancellationRequested)
            return Task.CompletedTask;

        var status = problem?.Status ?? context.Response.StatusCode;
        var defaults = TypedResults.Problem(statusCode: status).ProblemDetails;
        defaults.Instance = context.Request.PathBase.Add(context.Request.Path).Value;
        problem ??= new ProblemDetails();
        ApplyScopeDefaults(problem);
        problem.Status = status;
        problem.Type ??= defaults.Type ?? "about:blank";
        problem.Title ??= defaults.Title ?? "An error occurred while processing the request.";
        problem.Instance ??= defaults.Instance;
        problem.Extensions["requestId"] = context.TraceIdentifier;

        // Serialize before committing headers so a faulty consumer extension/converter can fail safely.
        byte[] body;
        try
        {
            body = JsonSerializer.SerializeToUtf8Bytes(problem, problem.GetType(), JsonConventions.DefaultOptions);
        }
        catch (Exception) when (!context.RequestAborted.IsCancellationRequested)
        {
            ScopeContext.Log?.AddWarning("API Problem Details serialization failed. A generic response was returned.");
            defaults.Type ??= "about:blank";
            defaults.Title ??= "An error occurred while processing the request.";
            defaults.Extensions["requestId"] = context.TraceIdentifier;
            body = JsonSerializer.SerializeToUtf8Bytes(defaults, JsonConventions.DefaultOptions);
        }

        context.Response.StatusCode = status;
        context.Response.ContentLength = null;
        context.Response.ContentType = "application/problem+json";
        return HttpMethods.IsHead(context.Request.Method)
            ? Task.CompletedTask
            : context.Response.Body.WriteAsync(body, context.RequestAborted).AsTask();
    }

    private static void ApplyScopeDefaults(ProblemDetails problem)
    {
        var scoped = ScopeContext.CurrentProblem;
        if (scoped == null)
            return;

        problem.Type ??= scoped.Type;
        problem.Title ??= scoped.Title;
        problem.Detail ??= scoped.Detail;
        problem.Instance ??= scoped.Instance;
        foreach (var (key, value) in scoped.Extensions)
        {
            // Standard fields and validation errors have dedicated owners.
            if (key is "type" or "title" or "status" or "detail" or "instance" or "requestId" or "errors")
                continue;

            problem.Extensions.TryAdd(key, value);
        }
    }
}
