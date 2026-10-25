using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;

namespace DRN.Framework.Hosting.Middlewares.ExceptionHandler;

[Transient<ErrorPageRenderer>]
public class ErrorPageRenderer(IPageUtils pageUtils, IOptions<DrnErrorPageOptions> options)
{
    public bool CanRender(HttpContext context) =>
        GetPageName(context.Response.StatusCode) != null && WantsHtml(context);

    private string? GetPageName(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => options.Value.BadRequestPage,
        StatusCodes.Status404NotFound => options.Value.NotFoundPage,
        >= 500 and <= 599 => options.Value.ServerErrorPage,
        _ => null
    };

    public static bool WantsHtml(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<IApiBehaviorMetadata>() == null &&
        context.Request.GetTypedHeaders().Accept?.Any(value =>
            value.MediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase) &&
            value.Quality.GetValueOrDefault(1) > 0) == true;

    public async Task WriteErrorAsync(HttpContext context)
    {
        var pageName = GetPageName(context.Response.StatusCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(pageName);
        var pagePath = $"/Pages{pageName}.cshtml";
        var html = context.Response.StatusCode switch
        {
            StatusCodes.Status400BadRequest => await pageUtils.RenderPageAsync(pagePath,
                new BadRequestPageModel { RequestId = context.TraceIdentifier }, context),
            StatusCodes.Status404NotFound => await pageUtils.RenderPageAsync(pagePath,
                new NotFoundPageModel { RequestId = context.TraceIdentifier }, context),
            _ => await pageUtils.RenderPageAsync(pagePath,
                new ServerErrorPageModel { RequestId = context.TraceIdentifier }, context)
        };
        context.Response.ContentLength = null;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store, no-cache";
        await context.Response.WriteAsync(html, context.RequestAborted);
    }
}
