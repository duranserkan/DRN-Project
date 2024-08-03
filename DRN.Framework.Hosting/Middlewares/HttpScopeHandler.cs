using DRN.Framework.SharedKernel;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using Flurl.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DRN.Framework.Hosting.Middlewares;

//https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/write?view=aspnetcore-8.0
//https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-8.0
//https://github.com/serilog/serilog/wiki/Structured-Data
public class HttpScopeHandler(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, IScopedLog scopedLog, ILogger<HttpScopeHandler> logger, IAppSettings appSettings)
    {
        try
        {
            await next(httpContext);
            PrepareScopeLog(httpContext, scopedLog);
        }
        catch (Exception e)
        {
            httpContext.Response.StatusCode = GetHttpStatusCode(e);
            if (e is FlurlHttpException f)
                await f.PrepareScopeLogForFlurlExceptionAsync(scopedLog, appSettings.Features);

            scopedLog.AddException(e);
            PrepareScopeLog(httpContext, scopedLog);

            //todo: integrate developer exception page
            //https://github.com/dotnet/aspnetcore/blob/main/src/Middleware/Diagnostics/src/DeveloperExceptionPage/DeveloperExceptionPageMiddleware.cs
            if (httpContext.Response.StatusCode is < 100 or > 599)
                httpContext.Abort();
            else if (appSettings.IsDevEnvironment)
                await httpContext.Response.WriteAsJsonAsync(scopedLog.Logs);
            else
                await httpContext.Response.WriteAsync($"TraceId: {httpContext.TraceIdentifier}");
        }
        finally
        {
            logger.LogScoped(scopedLog);
        }
    }

    private static int GetHttpStatusCode(Exception e)
    {
        return e switch
        {
            DrnException dEx => dEx.Status,
            FlurlHttpException fEx => fEx.GetGatewayStatusCode(),
            _ => 500
        };
    }

    private static void PrepareScopeLog(HttpContext httpContext, IScopedLog scopedLog) => scopedLog
        .WithLoggerName(nameof(HttpScopeHandler))
        .WithTraceIdentifier(httpContext.TraceIdentifier)
        .Add("l5d-client-id", httpContext.Request.Headers.TryGetValue("l5d-client-id", out var l5dId) ? l5dId.ToString() : string.Empty)
        .Add("HttpProtocol", httpContext.Request.Protocol.Split('/')[^1])
        .Add("HttpMethod", httpContext.Request.Method)
        .Add("HttpScheme", httpContext.Request.Scheme)
        .Add("RequestHost", httpContext.Request.Host.ToString())
        .Add("RequestPath", httpContext.Request.Path.ToString())
        .Add("RequestQueryString", httpContext.Request.QueryString.ToString())
        .Add("RequestContentLength", httpContext.Request.ContentLength ?? 0)
        .Add("RequestIpAddress", httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty)
        .Add("ResponseStatusCode", httpContext.Response.StatusCode);
}