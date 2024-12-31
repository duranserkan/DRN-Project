// This file is licensed to you under the MIT license.

using System.Diagnostics;
using DRN.Framework.Hosting.Areas.Developer.Pages;
using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.Middlewares.ExceptionHandler;

public record ExceptionContentResult(int StatusCode, string ContentType, string Content);

public interface IExceptionPageContentProvider
{
    Task<ExceptionContentResult> CreatErrorContentResult(HttpContext httpContext, Exception exception, DrnExceptionModel model);
}

[Transient<IExceptionPageContentProvider>]
public class ExceptionPageContentProvider(
    IPageUtils pageUtils,
    IExceptionUtils exceptionUtils)
    : IExceptionPageContentProvider
{
    // Assumes the response headers have not been sent.  If they have, still attempt to write to the body.
    public async Task<ExceptionContentResult> CreatErrorContentResult(HttpContext httpContext, Exception exception, DrnExceptionModel model)
    {
        // We need to inform the debugger that this exception should be considered user-unhandled since it wasn't fully handled by an exception filter.
        Debugger.BreakForUserUnhandledException(exception);


        if (exception is ICompilationException compilationException)
            return await CreateCompilationErrorContentResult(httpContext, exception, compilationException);

        return await CreateHtmlErrorContentResult(httpContext, exception, model);
    }

    public async Task<ExceptionContentResult> CreateHtmlErrorContentResult(HttpContext context, Exception exception, DrnExceptionModel model)
    {
        var exceptionPage = new RuntimeExceptionPage { ErrorModel = model };
        var pageResult = await pageUtils.RenderPageAsync(ExceptionPageAccessor.RuntimeExceptionPagePath, exceptionPage, context);

        var statusCode = context.Response.StatusCode < 1 ? 500 : context.Response.StatusCode;
        var result = new ExceptionContentResult(statusCode, "text/html; charset=utf-8", pageResult);

        return result;
    }

    public async Task<ExceptionContentResult> CreateCompilationErrorContentResult(HttpContext context, Exception exception, ICompilationException compilationException)
    {
        var exceptionPage = new CompilationExceptionPage { ErrorModel = exceptionUtils.CreateCompilationErrorModel(context, exception, compilationException) };
        var pageResult = await pageUtils.RenderPageAsync(ExceptionPageAccessor.CompilationExceptionPagePath, exceptionPage);

        var statusCode = context.Response.StatusCode < 1 ? 500 : context.Response.StatusCode;
        var result = new ExceptionContentResult(statusCode, "text/html; charset=utf-8", pageResult);

        return result;
    }
}