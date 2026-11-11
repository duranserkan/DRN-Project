using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace DRN.Framework.Hosting.DrnProgram;

internal static class OpenApiDocumentEndpoints
{
    internal const string DocumentEndpointName = "DRN.OpenApi.Document";

    internal static string GetDocumentUrl(HttpContext context, DrnProgramSwaggerOptions options) =>
        GetDocumentPath(context.RequestServices.GetRequiredService<LinkGenerator>(), options, context.Request.PathBase);

    internal static bool ConfigureSwaggerDocumentUrl(SwaggerUIOptions ui, DrnProgramSwaggerOptions options)
    {
        if (ui.ConfigObject.Urls is not null) return false;
        // Resolve the named document at request time, after endpoint data sources are registered.
        ui.SwaggerEndpoint("./openapi-document.json", options.Title);
        return true;
    }

    private static string GetDocumentPath(LinkGenerator links, DrnProgramSwaggerOptions options, PathString pathBase) =>
        links.GetPathByName(DocumentEndpointName,
            new { documentName = options.OpenApiInfo.Version, extension = "json" }, pathBase: pathBase)
        ?? throw new InvalidOperationException("Cannot generate the configured OpenAPI document URL.");
}
