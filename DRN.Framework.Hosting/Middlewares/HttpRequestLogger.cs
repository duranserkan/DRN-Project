using DRN.Framework.Hosting.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DRN.Framework.Hosting.Middlewares;

//https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/write?view=aspnetcore-8.0
//https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-8.0
//https://github.com/serilog/serilog/wiki/Structured-Data
//https://github.com/NLog/NLog/wiki/How-to-use-structured-logging
public class HttpRequestLogger(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, ILogger<HttpRequestLogger> logger)
    {
        var originalBodyStream = httpContext.Response.Body;
        using var responseBodyStream = new MemoryStream();
        httpContext.Response.Body = responseBodyStream;

        try
        {
            var requestHeader = httpContext.Request.Headers.ConvertToString();
            var requestBody = await ReadRequestBody(httpContext.Request);
            logger.LogInformation("""
                                  (Request)TraceIdentifier: {TraceIdentifier}
                                  {RequestHeader}
                                  {RequestBody}
                                  """, httpContext.TraceIdentifier, requestHeader, requestBody);

            await next(httpContext);
        }
        finally
        {
            var responseHeader = httpContext.Response.Headers.ConvertToString();
            var responseBody = await ReadResponseBody(responseBodyStream, originalBodyStream);
            logger.LogInformation("""
                                  (Response)TraceIdentifier: {TraceIdentifier}

                                  {ResponseHeader}

                                  {ResponseBody}
                                  """, httpContext.TraceIdentifier, responseHeader, responseBody);
        }
    }

    private static async Task<string> ReadRequestBody(HttpRequest request)
    {
        request.EnableBuffering();
        var body = await new StreamReader(request.Body).ReadToEndAsync();
        request.Body.Seek(0, SeekOrigin.Begin);

        return body;
    }

    private static async Task<string> ReadResponseBody(MemoryStream responseBody, Stream originalBodyStream)
    {
        // Capture the response body
        responseBody.Seek(0, SeekOrigin.Begin);
        var responseBodyString = await new StreamReader(responseBody).ReadToEndAsync();

        // Copy the response body back to the original stream
        responseBody.Seek(0, SeekOrigin.Begin);
        await responseBody.CopyToAsync(originalBodyStream);

        return responseBodyString;
    }
}