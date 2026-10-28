using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Hosting.Middlewares.ApiErrors;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler;
using DRN.Framework.SharedKernel.Json;
using DRN.Framework.Utils.DependencyInjection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DRN.Framework.Hosting;

public static class HostingModule
{
    public static IServiceCollection AddDrnHosting(this IServiceCollection sc, DrnProgramSwaggerOptions options, IConfiguration configuration)
    {
        //https://andrewlock.net/extending-the-shutdown-timeout-setting-to-ensure-graceful-ihostedservice-shutdown/
        //https://learn.microsoft.com/en-us/dotnet/core/extensions/options
        sc.Configure<HostOptions>(configuration.GetSection("HostOptions"));
        sc.AddOptions<DrnErrorPageOptions>()
            .Validate(options => DrnErrorPageOptions.IsValidPageName(options.BadRequestPage),
                "BadRequestPage must be a Razor page name such as /BadRequest, without .cshtml, or null.")
            .Validate(options => DrnErrorPageOptions.IsValidPageName(options.ServerErrorPage),
                "ServerErrorPage must be a Razor page name such as /Errors/Server, without .cshtml, or null.")
            .Validate(options => DrnErrorPageOptions.IsValidPageName(options.NotFoundPage),
                "NotFoundPage must be a Razor page name such as /NotFound, without .cshtml, or null.")
            .ValidateOnStart();
        sc.ConfigureHttpJsonOptions(jsonOptions => JsonConventions.SetJsonDefaults(jsonOptions.SerializerOptions));
        sc.AddOptions<DrnApiErrorOptions>()
            .Validate(apiOptions => apiOptions.PathPrefixes != null && apiOptions.PathPrefixes.All(DrnApiErrorOptions.IsValidPrefix),
                "API path prefixes must be absolute application paths such as /Api, without a trailing slash, query, or fragment.")
            .ValidateOnStart();
        sc.Configure<MvcOptions>(mvcOptions => mvcOptions.Filters.AddService<DrnApiProblemDetailsFilter>(int.MaxValue));
        sc.AddLogging();
        sc.AddEndpointsApiExplorer();
        sc.AddHttpContextAccessor();

        sc.AddServicesWithAttributes();
        sc.AddProblemDetails();
        if (options.AddSwagger)
            sc.AddSwaggerGen(options.ConfigureSwaggerGenOptions);

        return sc;
    }
}
