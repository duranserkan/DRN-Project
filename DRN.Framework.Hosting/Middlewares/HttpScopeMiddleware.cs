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

//https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/write?view=aspnetcore-8.0
//https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-8.0
//https://github.com/serilog/serilog/wiki/Structured-Data
public class HttpScopeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IScopedLog scopedLog, IScopedUser scopedUser, IServiceProvider serviceProvider,
        ILogger<HttpScopeMiddleware> logger, IAppSettings appSettings, IDrnExceptionHandler exceptionHandler)
    {
        try
        {
            if (ExceptionPageAccessor.IsExceptionPage(context.Request.Path.Value))
            {
                context.Abort(); //Requesting exception pages are malicious. Exception pages are rendered when exception occurs.
                return;
            }

            context.Request.EnableBuffering();
            PrepareScopeLog(context, scopedLog);
            ScopeContext.Initialize(context.TraceIdentifier, scopedLog, scopedUser, appSettings, serviceProvider);
            await next(context);
            scopedLog.Add("ResponseStatusCode", context.Response.StatusCode);
        }
        catch (Exception e)
        {
            context.Response.StatusCode = GetHttpStatusCode(e);
            if (e is FlurlHttpException f)
                await f.PrepareScopeLogForFlurlExceptionAsync(scopedLog, appSettings.Features);
            
            scopedLog.AddException(e);
            scopedLog.Add("ResponseStatusCode", context.Response.StatusCode);

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
            logger.LogScoped(scopedLog);
            if (!context.Response.Headers.ContainsKey("Cache-Control")) // Add headers to prevent caching
            {
                context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
                context.Response.Headers.Pragma = "no-cache";
                context.Response.Headers.Expires = "0";
            }

            //If you need to preserve the original HTTP method during redirection,
            //the correct status code to use is 307 (Temporary Redirect) or 308 (Permanent Redirect)
            //Use 303 instead of 302 for redirects because it is explicit, adheres to modern HTTP standards,
            //ensures predictable behavior across all clients, and clearly communicates your intent to convert the request method.
            if (context.Response.StatusCode == 302)
                context.Response.StatusCode = 303;
        }
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
        .Add("l5d-client-id", httpContext.Request.Headers.TryGetValue("l5d-client-id", out var l5dId) ? l5dId.ToString() : string.Empty)
        .Add("HttpProtocol", httpContext.Request.Protocol.Split('/')[^1])
        .Add("HttpMethod", httpContext.Request.Method)
        .Add("HttpScheme", httpContext.Request.Scheme)
        .Add("RequestHost", httpContext.Request.Host.ToString())
        .Add("RequestPath", httpContext.Request.Path.ToString())
        .Add("RequestQueryString", httpContext.Request.QueryString.ToString())
        .Add("RequestContentLength", httpContext.Request.ContentLength ?? 0)
        .Add("RequestIpAddress", httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty);
}