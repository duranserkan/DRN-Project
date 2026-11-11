using System.Globalization;
using DRN.Framework.SharedKernel;
using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.Hosting.Middlewares.ApiErrors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace DRN.Framework.Hosting.DrnProgram;

public class DrnProgramSwaggerOptions
{
    internal PathString? SwaggerUIPathPrefix { get; private set; }
    private bool _metadataConfigured;

    internal void ConfigureMetadata()
    {
        if (_metadataConfigured) return;
        ConfigureOpenApiInfo?.Invoke(OpenApiInfo);
        _metadataConfigured = true;
    }

    internal void ConfigureSwaggerUI(SwaggerUIOptions options, DrnPageOptions? pageOptions = null)
    {
        ConfigureMetadata();
        options.DocumentTitle = Title;
        ConfigureSwaggerUIOptionsAction?.Invoke(options);
        options.RoutePrefix = ValidateRoutePrefix(options.RoutePrefix, "Swagger UI");
        SwaggerUIPathPrefix = new PathString("/" + options.RoutePrefix);
        options.HeadContent += pageOptions?.GetFaviconHeadContent(options.RoutePrefix);
    }

    public bool AddSwagger { get; set; }

    /// <summary>Enables Swagger UI when AddSwagger is enabled.</summary>
    public bool AddSwaggerUI { get; set; } = true;

    private static string ValidateRoutePrefix(string? routePrefix, string ui)
    {
        var prefix = routePrefix?.Trim('/');
        if (string.IsNullOrEmpty(prefix))
            throw new ConfigurationException($"{ui} RoutePrefix must be nonempty after trimming slashes. Root mounts are not supported.");
        if (prefix.Split('/').Any(segment =>
                segment.Length == 0 || segment.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_')))
            throw new ConfigurationException($"{ui} RoutePrefix must contain nonempty path segments using letters, digits, hyphens or underscores.");

        return prefix;
    }

    /// <summary>
    /// Optional named policy required by the Swagger UI, its assets, and OpenAPI documents.
    /// Null uses the application's fallback authorization policy.
    /// </summary>
    [Sensitive(false, Justification = "Authorization policy name, not a credential.")]
    public string? AuthorizationPolicy { get; set; }

    /// <summary>
    /// Builds the Swagger server URL from the processed request's Scheme, Host, and PathBase.
    /// Forwarded headers must be applied by middleware before the document is served.
    /// </summary>
    public bool ApplyTargetServerForwardedHeadersCorrection { get; set; } = true;
    [Sensitive(false, Justification = "OpenAPI security configuration flag.")]
    public bool AddBearerTokenSecurityRequirement { get; set; } = true;

    private string? _title;

    /// <summary>Browser-tab title. Defaults to the API title (application name) and OpenAPI version.</summary>
    public string Title
    {
        get => _title ?? $"{OpenApiInfo.Title ?? AppConstants.EntryAssemblyName} ({OpenApiInfo.Version})";
        set => _title = value;
    }
    public string DefaultRouteTemplate { get; set; } = "/swagger/{documentName}/swagger.{extension:regex(^(json|ya?ml)$)}";
    public OpenApiInfo OpenApiInfo { get; set; } = new() { Version = "v1" };

    public Action<OpenApiInfo>? ConfigureOpenApiInfo { get; set; } = null;
    public Action<SwaggerGenOptions>? ConfigureSwaggerGenOptionsAction { get; set; } = null;
    public Action<SwaggerUIOptions>? ConfigureSwaggerUIOptionsAction { get; set; } = null;
    public Action<SwaggerEndpointOptions>? ConfigureSwaggerEndpointOptionsAction { get; set; } = null;

    internal void ConfigureSwaggerEndpointOptions(SwaggerEndpointOptions options)
    {
        if (ApplyTargetServerForwardedHeadersCorrection)
            options.PreSerializeFilters.Add(SwaggerProxyPathFilter);

        options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_2;

        ConfigureSwaggerEndpointOptionsAction?.Invoke(options);
    }

    internal void ConfigureSwaggerGenOptions(SwaggerGenOptions options)
    {
        ConfigureMetadata();
        options.SwaggerDoc(OpenApiInfo.Version, OpenApiInfo);
        // Swashbuckle unwraps Nullable<T> for type mappings and adds null at the usage site.
        options.MapType<long>(CreateInt64Schema);
        options.SchemaFilter<PaginationSchemaFilter>();
        options.OperationFilter<DrnApiErrorOperationFilter>();
        options.OperationFilter<PaginationQueryOperationFilter>();

        if (AddBearerTokenSecurityRequirement)
            BearerTokenSecurityRequirement(options);

        ConfigureSwaggerGenOptionsAction?.Invoke(options);
    }

    private static OpenApiSchema CreateInt64Schema() => new()
    {
        Description = "Accepts a signed 64-bit integer as a JSON number or decimal string. Responses use numbers within the JavaScript safe integer range and decimal strings outside it.",
        OneOf =
        [
            new OpenApiSchema
            {
                Type = JsonSchemaType.Integer, Format = "int64",
                Minimum = long.MinValue.ToString(CultureInfo.InvariantCulture),
                Maximum = long.MaxValue.ToString(CultureInfo.InvariantCulture)
            },
            new OpenApiSchema
            {
                Type = JsonSchemaType.String, Format = "int64"
            }
        ]
    };

    private static void BearerTokenSecurityRequirement(SwaggerGenOptions options)
    {
        options.OperationFilter<AnonymousOperationSecurityFilter>();
        options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            In = ParameterLocation.Header,
            Name = "Authorization",
            Scheme = "bearer",
            Description = "Please enter token",
        });
        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("bearer", document)] = []
        });
    }

    private void SwaggerProxyPathFilter(OpenApiDocument swaggerDoc, HttpRequest httpRequest)
    {
        var url = UriHelper.BuildAbsolute(httpRequest.Scheme, httpRequest.Host, httpRequest.PathBase);

        swaggerDoc.Servers = new List<OpenApiServer> { new() { Url = url } };
    }
}
