// This file is licensed to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.Middlewares.ExceptionHandler;

public interface IDrnExceptionHandler
{
    Task HandleExceptionAsync(HttpContext context, Exception ex);
}

[Scoped<IDrnExceptionHandler>]
public class DrnExceptionHandler(
    IScopedLog scopedLog,
    IAppSettings appSettings,
    IExceptionUtils exceptionUtils,
    IEnumerable<IDrnExceptionFilter> filters,
    IExceptionPageContentProvider contentProvider,
    DiagnosticSource diagnosticSource)
    : IDrnExceptionHandler
{
    public async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        if (context.Response.HasStarted || IsRequestCancelled(context, ex)) return;

        try
        {
            await RenderErrorPageAsync(context, ex);
        }
        catch (Exception e2)
        {
            _ = e2;
            scopedLog.Add("ExceptionPageErrorType", e2.GetType().FullName ?? e2.GetType().Name);
            scopedLog.Add("ExceptionPageErrorMessage", e2.Message);
            scopedLog.Add("ExceptionPageErrorStackTrace", e2.StackTrace ?? string.Empty);
            await context.Response.WriteAsJsonAsync(scopedLog.Logs);
        }

        const string eventName = "Microsoft.AspNetCore.Diagnostics.UnhandledException";
        if (diagnosticSource.IsEnabled(eventName))
            WriteDiagnosticEvent(diagnosticSource, eventName, new { httpContext = context, exception = ex });
    }


    private static bool IsRequestCancelled(HttpContext context, Exception ex)
    {
        if (ex is not (OperationCanceledException or IOException) || !context.RequestAborted.IsCancellationRequested) return false;

        if (!context.Response.HasStarted)
            context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;

        // Generally speaking, we do not expect application code to handle things like IOExceptions during a request
        // body read due to a client disconnect. But aborted requests should be rare in development, and developers
        // might be surprised if an IOException propagating through their code was not considered user-unhandled.
        // That said, if developers complain, we consider removing the following line.
        Debugger.BreakForUserUnhandledException(ex);
        return true;
    }

    private async Task RenderErrorPageAsync(HttpContext context, Exception exception)
    {
        var skipPostCreationFilter = false;
        foreach (var filter in filters)
        {
            var preFilterResult = await filter.HandlePreExceptionModelCreationAsync(context, exception);
            if (preFilterResult.SkipExceptionHandling) 
                return;
            if (preFilterResult.SkipPostCreationFilter) 
                skipPostCreationFilter = true;
        }

        DrnExceptionModel model = null!;
        if (!skipPostCreationFilter)
        {
            model = await exceptionUtils.CreateErrorPageModelAsync(context, exception);
            foreach (var filter in filters)
            {
                var postFilterResult = await filter.HandleExceptionAsync(context, exception, model);
                if (postFilterResult.SkipExceptionHandling) return;
            }
        }
        
        if (appSettings.IsDevEnvironment)
        {
            //todo: Database error page
            //https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-9.0#database-error-page
            var result = await contentProvider.CreatErrorContentResult(context, exception, model);

            context.Response.StatusCode = result.StatusCode;
            context.Response.ContentType = result.ContentType;

            await context.Response.WriteAsync(result.Content);
            return;
        }

        //todo: prod exception page
        //https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-9.0#exception-handler-page

        var statusCode = ((HttpStatusCode)context.Response.StatusCode).ToString();
        await context.Response.WriteAsync($"{statusCode} {context.Response.StatusCode} TraceId: {context.TraceIdentifier}");
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
        Justification = "The values being passed into Write have the commonly used properties being preserved with DynamicDependency.")]
    private static void WriteDiagnosticEvent<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(DiagnosticSource diagnosticSource,
        string name, TValue value) => diagnosticSource.Write(name, value);
}