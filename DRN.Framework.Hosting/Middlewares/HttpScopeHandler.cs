using DRN.Framework.SharedKernel;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Logging;
using Flurl.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DRN.Framework.Hosting.Middlewares;

//https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/write?view=aspnetcore-8.0
//https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-8.0
//https://github.com/serilog/serilog/wiki/Structured-Data
public class HttpScopeHandler(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, IScopedLog scopedLog, ILogger<HttpScopeHandler> logger)
    {
        try
        {
            await next(httpContext);
        }
        catch (Exception e)
        {
            scopedLog.AddException(e);
            httpContext.Response.StatusCode = e switch
            {
                DrnException dEx => dEx.Status,
                FlurlHttpException fEx => fEx.GetGatewayStatusCode(),
                _ => 500
            };
            if (httpContext.Response.StatusCode is > 99 and < 600)
                await httpContext.Response.WriteAsync($"TraceId: {httpContext.TraceIdentifier}");
            else
                httpContext.Abort();
        }
        finally
        {
            PrepareScopeLog(httpContext, scopedLog);
            logger.LogScoped(scopedLog);
        }
    }

    private static void PrepareScopeLog(HttpContext httpContext, IScopedLog scopedLog) => scopedLog
        .WithLoggerName(nameof(HttpScopeHandler))
        .WithTraceIdentifier(httpContext.TraceIdentifier)
        .Add("HttpProtocol", httpContext.Request.Protocol.Split('/').Last())
        .Add("HttpMethod", httpContext.Request.Method)
        .Add("RequestHost", httpContext.Request.Host.ToString())
        .Add("RequestPath", httpContext.Request.Path.ToString())
        .Add("RequestQueryString", httpContext.Request.QueryString.ToString())
        .Add("RequestContentLength", httpContext.Request.ContentLength ?? 0)
        .Add("RequestIpAddress", httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty)
        .Add("ResponseStatusCode", httpContext.Response.StatusCode);
}