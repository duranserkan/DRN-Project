// This file is licensed to you under the MIT license.

using System.Diagnostics;
using DRN.Framework.Hosting.Areas.Developer.Pages;
using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.Middlewares.ExceptionHandler;

public record ExceptionContentResult(string ContentType, string Content);

public interface IExceptionPageContentProvider
{
    Task<ExceptionContentResult> CreateErrorContentResult(HttpContext httpContext, Exception exception, DrnExceptionModel model);
    Task<ExceptionContentResult> CreateHtmlErrorContentResult(HttpContext context, DrnExceptionModel model);
}

[Transient<IExceptionPageContentProvider>]
public class ExceptionPageContentProvider(
    IPageUtils pageUtils,
    IExceptionUtils exceptionUtils,
    DrnDevelopmentSettings developmentSettings)
    : IExceptionPageContentProvider
{
    // Assumes the response headers have not been sent.  If they have, still attempt to write to the body.
    public async Task<ExceptionContentResult> CreateErrorContentResult(HttpContext httpContext, Exception exception, DrnExceptionModel model)
    {
        // We need to inform the debugger that this exception should be considered user-unhandled since it wasn't fully handled by an exception filter.
        if (developmentSettings.BreakForUserUnhandledException)
            Debugger.BreakForUserUnhandledException(exception);

        if (exception is ICompilationException compilationException)
            return await CreateCompilationErrorContentResult(httpContext, exception, compilationException);

        return await CreateHtmlErrorContentResult(httpContext, model);
    }

    public async Task<ExceptionContentResult> CreateHtmlErrorContentResult(HttpContext context, DrnExceptionModel model)
    {
        var exceptionPage = new RuntimeExceptionPage { ErrorModel = model };
        var pageResult = await pageUtils.RenderPageAsync(ExceptionPageAccessor.RuntimeExceptionPagePath, exceptionPage, context);

        var result = new ExceptionContentResult("text/html; charset=utf-8", pageResult);

        return result;
    }

    public async Task<ExceptionContentResult> CreateCompilationErrorContentResult(HttpContext context, Exception exception, ICompilationException compilationException)
    {
        var exceptionPage = new CompilationExceptionPage { ErrorModel = exceptionUtils.CreateCompilationErrorModel(context, exception, compilationException) };
        var pageResult = await pageUtils.RenderPageAsync(ExceptionPageAccessor.CompilationExceptionPagePath, exceptionPage);

        var result = new ExceptionContentResult("text/html; charset=utf-8", pageResult);

        return result;
    }
}