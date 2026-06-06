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
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Hosting.Middlewares.ExceptionHandler;

public record ExceptionPageModel(HttpContext HttpContext, DrnExceptionModel? ExceptionModel);

public interface IDrnExceptionHandler
{
    Task HandleExceptionAsync(HttpContext context, Exception ex);
    Task<ExceptionPageModel> GetExceptionPageModel(IServiceProvider serviceProvider, Exception exception);
    Task<ExceptionContentResult?> GetExceptionContentAsync(IServiceProvider serviceProvider, Exception exception);
    Task<ExceptionContentResult?> GetStartupExceptionContentAsync(IServiceProvider serviceProvider, Exception exception, IScopedLog startupLog);
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

            if (!context.Response.HasStarted && appSettings.IsDevelopmentEnvironment)
                await context.Response.WriteAsJsonAsync(scopedLog.GetLogs());
            else if (!context.Response.HasStarted)
                await WriteProductionErrorResponseAsync(context);
        }

        const string eventName = "Microsoft.AspNetCore.Diagnostics.UnhandledException";
        if (diagnosticSource.IsEnabled(eventName))
            WriteDiagnosticEvent(diagnosticSource, eventName, new { httpContext = context, exception = ex });
    }

    public async Task<ExceptionContentResult?> GetExceptionContentAsync(IServiceProvider serviceProvider, Exception exception)
    {
        var exceptionPageModel = await GetExceptionPageModel(serviceProvider, exception);
        if (!appSettings.IsDevelopmentEnvironment)
            return null;

        var result = await GetExceptionContentResult(exceptionPageModel, exception);

        return result;
    }

    public async Task<ExceptionPageModel> GetExceptionPageModel(IServiceProvider serviceProvider, Exception exception)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = serviceProvider
        };

        //even in production we may want to execute IDrnExceptionFilter filters
        var model = await ExecuteExceptionPageModel(context, exception);

        return new ExceptionPageModel(context, model);
    }
    
    public async Task<ExceptionContentResult?> GetStartupExceptionContentAsync(IServiceProvider serviceProvider, Exception exception, IScopedLog startupLog)
    {
        using var requestServices = serviceProvider.CreateScope();
        var scopedProvider = requestServices.ServiceProvider;
        var activeScopedLog = scopedProvider.GetRequiredService<IScopedLog>();
        activeScopedLog.CopyFrom(startupLog);

        var result = await GetExceptionContentAsync(scopedProvider, exception);

        return result;
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
        context.Response.StatusCode = context.Response.StatusCode < 1 ? 500 : context.Response.StatusCode;

        //even in production we may want to execute IDrnExceptionFilter filters
        var model = await ExecuteExceptionPageModel(context, exception);
        if (!appSettings.IsDevelopmentEnvironment)
        {
            //todo: prod exception page, if error page is configured redirect to it
            //https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling#exception-handler-page
            await WriteProductionErrorResponseAsync(context);
            return;
        }

        var exceptionPageModel = new ExceptionPageModel(context, model);
        var result = await GetExceptionContentResult(exceptionPageModel, exception);
        if (result != null)
        {
            context.Response.ContentType = result.ContentType;
            await context.Response.WriteAsync(result.Content);
            return;
        }

        await WriteProductionErrorResponseAsync(context);
    }

    private static async Task WriteProductionErrorResponseAsync(HttpContext context)
    {
        context.Response.ContentType = "text/plain; charset=utf-8";
        var statusCode = ((HttpStatusCode)context.Response.StatusCode).ToString();
        await context.Response.WriteAsync($"{statusCode} {context.Response.StatusCode} TraceId: {context.TraceIdentifier}");
    }

    private async Task<ExceptionContentResult?> GetExceptionContentResult(ExceptionPageModel pageModel, Exception exception)
    {
        var exceptionModel = pageModel.ExceptionModel;
        if (exceptionModel == null)
            return null;

        //https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-9.0#database-error-page
        var result = await contentProvider.CreateErrorContentResult(pageModel.HttpContext, exception, exceptionModel);
        return result;
    }

    private async Task<DrnExceptionModel?> ExecuteExceptionPageModel(HttpContext context, Exception exception)
    {
        var skipPostCreationFilter = false;
        foreach (var filter in filters)
        {
            var preFilterResult = await filter.HandlePreExceptionModelCreationAsync(context, exception);
            if (preFilterResult.SkipExceptionHandling)
                return null;
            if (preFilterResult.SkipPostCreationFilter)
                skipPostCreationFilter = true;
        }

        var model = await exceptionUtils.CreateErrorPageModelAsync(context, exception);

        if (skipPostCreationFilter)
            return model;

        foreach (var filter in filters)
        {
            var postFilterResult = await filter.HandleExceptionAsync(context, exception, model);
            if (postFilterResult.SkipExceptionHandling)
                return model;
        }

        return model;
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
        Justification = "The values being passed into Write have the commonly used properties being preserved with DynamicDependency.")]
    private static void WriteDiagnosticEvent<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(DiagnosticSource diagnosticSource,
        string name, TValue value)
    {
        try
        {
            diagnosticSource.Write(name, value);
        }
        catch
        {
            // ignored
        }
    }
}
