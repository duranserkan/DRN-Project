using DRN.Framework.Utils.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DRN.Framework.Hosting.Middlewares;

//https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/write?view=aspnetcore-8.0
//https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-8.0
//https://github.com/NLog/NLog/wiki/How-to-use-structured-logging
public class HttpScopeLogger(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, IScopedLog scopedLog, ILogger<HttpScopeLogger> logger)
    {
        try
        {
            await next(httpContext);
        }
        catch (Exception e)
        {
            scopedLog.AddException(e);
            throw;
        }
        finally
        {
            PrepareScopeLog(httpContext, scopedLog);

            if (scopedLog.HasException)
                logger.LogError("{@Logs}", scopedLog.Logs);
            else if (scopedLog.HasWarning)
                logger.LogWarning("{@Logs}", scopedLog.Logs);
            else
                logger.LogInformation("{@Logs}", scopedLog.Logs);
        }
    }

    private static void PrepareScopeLog(HttpContext httpContext, IScopedLog scopedLog) =>
        scopedLog.WithLoggerName(nameof(HttpScopeLogger))
            .Add(nameof(httpContext.TraceIdentifier), httpContext.TraceIdentifier)
            .Add("RequestProtocol", httpContext.Request.Protocol)
            .Add("RequestHttpMethod", httpContext.Request.Method)
            .Add("RequestHost", httpContext.Request.Host.ToString())
            .Add("RequestPath", httpContext.Request.Path.ToString())
            .Add("RequestQueryString", httpContext.Request.QueryString.ToString())
            .Add("RequestContentLength", httpContext.Request.ContentLength ?? 0)
            .Add("RequestIpAddress", httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty)
            .Add("ResponseStatusCode", httpContext.Response.StatusCode);
}