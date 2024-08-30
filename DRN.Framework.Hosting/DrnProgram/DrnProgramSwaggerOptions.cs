using DRN.Framework.SharedKernel;
using Flurl;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace DRN.Framework.Hosting.DrnProgram;

public class DrnProgramSwaggerOptions
{
    public bool AddSwagger { get; set; }
    public bool ApplyTargetServerForwardedHeadersCorrection { get; set; } = true;
    public bool AddBearerTokenSecurityRequirement { get; set; } = true;

    public string Title { get; set; } = AppConstants.EntryAssemblyName;
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

        ConfigureSwaggerEndpointOptionsAction?.Invoke(options);
    }

    internal void ConfigureSwaggerGenOptions(SwaggerGenOptions options)
    {
        ConfigureOpenApiInfo?.Invoke(OpenApiInfo);
        options.SwaggerDoc(OpenApiInfo.Version, OpenApiInfo);

        if (AddBearerTokenSecurityRequirement)
            BearerTokenSecurityRequirement(options);

        ConfigureSwaggerGenOptionsAction?.Invoke(options);
    }

    private static void BearerTokenSecurityRequirement(SwaggerGenOptions options)
    {
        var openApiSecurityScheme = new OpenApiSecurityScheme
        {
            In = ParameterLocation.Header,
            Description = "Please enter token",
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        };

        options.AddSecurityDefinition("Bearer", openApiSecurityScheme);

        var requirement = new OpenApiSecurityRequirement { { openApiSecurityScheme, Array.Empty<string>() } };
        options.AddSecurityRequirement(requirement);
    }

    private void SwaggerProxyPathFilter(OpenApiDocument swaggerDoc, HttpRequest httpRequest)
    {
        var path = httpRequest.Path.ToString();
        var normalizedRequestPath = path.Contains("swagger")
            ? string.Empty
            : path;

        var url = new Url
        {
            Scheme = httpRequest.Scheme,
            Host = httpRequest.Headers.TryGetValue(ForwardedHeadersDefaults.XForwardedHostHeaderName, out var forwardedHostName)
                ? forwardedHostName.ToString()
                : httpRequest.Host.ToString(),
            Path = httpRequest.Headers.TryGetValue(ForwardedHeadersDefaults.XForwardedPrefixHeaderName, out var forwardedPathPrefix)
                ? forwardedPathPrefix.ToString()
                : normalizedRequestPath
        };

        swaggerDoc.Servers = new List<OpenApiServer> { new() { Url = url } };
    }
}