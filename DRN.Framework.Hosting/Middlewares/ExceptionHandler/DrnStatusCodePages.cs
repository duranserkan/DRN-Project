using DRN.Framework.Hosting.Middlewares.ApiErrors;
using DRN.Framework.Utils.Logging;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Hosting.Middlewares.ExceptionHandler;

internal static class DrnStatusCodePages
{
    internal static async Task HandleAsync(StatusCodeContext statusContext)
    {
        var context = statusContext.HttpContext;
        var apiErrors = context.RequestServices.GetRequiredService<DrnApiErrorResponse>();
        if (await apiErrors.TryWriteAsync(context))
            return;

        var renderer = context.RequestServices.GetRequiredService<ErrorPageRenderer>();
        if (!renderer.CanRender(context))
            return;

        var statusCode = context.Response.StatusCode;
        try
        {
            await renderer.WriteErrorAsync(context);
        }
        catch (Exception exception) when (!context.Response.HasStarted && !context.RequestAborted.IsCancellationRequested)
        {
            context.RequestServices.GetRequiredService<IScopedLog>().AddException(exception);
            context.Response.StatusCode = statusCode;
            await DrnExceptionHandler.WriteSafeErrorResponseAsync(context);
        }
    }
}
