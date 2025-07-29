using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler;
using DRN.Framework.SharedKernel;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Scope;
using DRN.Framework.Utils.Settings;
using Flurl.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DRN.Framework.Hosting.Middlewares;

//https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/write
//https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/
//https://github.com/serilog/serilog/wiki/Structured-Data
//todo: Model Capture in OnActionExecuting for auditing and observability purposes
public class HttpScopeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IScopedLog scopedLog, IScopedUser scopedUser, IServiceProvider serviceProvider,
        ILogger<HttpScopeMiddleware> logger, IAppSettings appSettings, IDrnExceptionHandler exceptionHandler)
    {
        try
        {
            //todo manage request buffering it may not be desired in production
            //It is currently required to obtain detailed exception report which reads request body
            context.Request.EnableBuffering();
            ResponseControls(context);

            if (ExceptionPageAccessor.IsExceptionPage(context.Request.Path.Value))
            {
                context.Abort(); //Requesting exception pages are malicious. Exception pages are rendered when an exception occurs.
                return;
            }

            PrepareScopeLog(context, scopedLog);
            ScopeContext.Initialize(context.TraceIdentifier, scopedLog, scopedUser, appSettings, serviceProvider);
            await next(context);
        }
        catch (Exception e)
        {
            context.Response.StatusCode = GetHttpStatusCode(e);
            if (e is FlurlHttpException f)
                await f.PrepareScopeLogForFlurlExceptionAsync(scopedLog, appSettings.Features);

            scopedLog.AddException(e);

            if (context.Response.StatusCode is < 100 or > 599) //MaliciousRequestException
            {
                context.Abort();
                return;
            }

            if (context.Response.HasStarted)
                scopedLog.Add("ResponseStarted", true);

            await exceptionHandler.HandleExceptionAsync(context, e);
        }
        finally
        {
            scopedLog.Add("ResponseStatusCode", context.Response.StatusCode);
            scopedLog.Add("ResponseContentLength", context.Response.ContentLength ?? 0);
            logger.LogScoped(scopedLog);

            //If you need to preserve the original HTTP method during redirection,
            //the correct status code to use is 307 (Temporary Redirect) or 308 (Permanent Redirect)
            //Use 303 instead of 302 for redirects because it is explicit, adheres to modern HTTP standards,
            //ensures predictable behavior across all clients, and clearly communicates your intent to convert the request method.
            if (context.Response.StatusCode == 302)
                context.Response.StatusCode = 303;
        }
    }

    private static void ResponseControls(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Request.Headers;
            if (headers.ContainsKey("Cache-Control"))
                return Task.CompletedTask;

            // Add headers to prevent caching
            headers.CacheControl = "no-store, no-cache, must-revalidate";
            headers.Pragma = "no-cache";
            headers.Expires = "0";

            return Task.CompletedTask;
        });
    }

    private static int GetHttpStatusCode(Exception e)
    {
        return e switch
        {
            DrnException dEx => dEx.Status,
            FlurlHttpException fEx => fEx.GetGatewayStatusCode(),
            BadHttpRequestException bEx => bEx.StatusCode,
            _ => 500
        };
    }

    private static void PrepareScopeLog(HttpContext httpContext, IScopedLog scopedLog) => scopedLog
        .WithLoggerName(nameof(HttpScopeMiddleware))
        .WithTraceIdentifier(httpContext.TraceIdentifier)
        .Add("RequestContentLength", httpContext.Request.ContentLength ?? 0)
        .Add("HttpProtocol", httpContext.Request.Protocol.Split('/')[^1])
        .Add("HttpMethod", httpContext.Request.Method)
        .Add("HttpScheme", httpContext.Request.Scheme)
        .Add("RequestHost", httpContext.Request.Host.ToString())
        .Add("RequestPath", httpContext.Request.Path.ToString())
        .AddIfNotNullOrEmpty("RequestQueryString", httpContext.Request.QueryString.ToString())
        .AddIfNotNullOrEmpty("RequestIpAddress", httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty)
        .AddIfNotNullOrEmpty("l5d-client-id",
            httpContext.Request.Headers.TryGetValue("l5d-client-id", out var l5dId)
                ? l5dId.ToString()
                : string.Empty);
}