using DRN.Framework.Hosting.Middlewares;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler;
using DRN.Framework.Hosting.Utils.Vite;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DRN.Framework.Hosting.DrnProgram.Configurators;

// The program supplies its virtual hooks; this class owns only their ordering and default middleware stages.
internal sealed class DrnPipelineConfigurator
{
    internal required Action<WebApplication, IAppSettings> PipelineStart { get; init; }
    internal required Action<WebApplication, IAppSettings> PreScopeStart { get; init; }
    internal required Action<WebApplication, IAppSettings> PostScopeStart { get; init; }
    internal required Action<WebApplication, IAppSettings> PreAuthentication { get; init; }
    internal required Action<WebApplication, IAppSettings> PostAuthentication { get; init; }
    internal required Action<WebApplication, IAppSettings> PostAuthorization { get; init; }
    internal required Action<WebApplication, IAppSettings> MapEndpoints { get; init; }
    internal required Func<IServiceProvider, IAppSettings, RateLimiterOptions> CreatePostAuthRateLimiterOptions { get; init; }

    internal void Configure(WebApplication application, IAppSettings appSettings)
    {
        PipelineStart(application, appSettings);

        PreScopeStart(application, appSettings);
        application.UseMiddleware<HttpScopeMiddleware>();
        PostScopeStart(application, appSettings);

        application.UseStatusCodePages(DrnStatusCodePages.HandleAsync);

        application.UseRouting();

        if (!appSettings.Features.RateLimit.Disabled)
            application.UseMiddleware<PreAuthRateLimitingMiddleware>();

        PreAuthentication(application, appSettings);
        application.UseAuthentication();
        application.UseMiddleware<ScopedUserMiddleware>();

        if (!appSettings.Features.RateLimit.Disabled)
            application.UseRateLimiter(CreatePostAuthRateLimiterOptions(application.Services, appSettings));

        PostAuthentication(application, appSettings);
        application.UseAuthorization();
        PostAuthorization(application, appSettings);

        MapEndpoints(application, appSettings);

        if (appSettings.DevelopmentSettings is { SkipValidation: false, TemporaryApplication: false })
        {
            var viteManifest = application.Services.GetRequiredService<IViteManifest>();
            _ = viteManifest.GetAllManifestItems();
        }
    }

    internal static void ConfigurePipelineStart(WebApplication application)
    {
        application.UseForwardedHeaders();
        application.UseHostFiltering();
        application.UseCookiePolicy();
        application.UseSecurityHeaders();
    }

    internal static void ConfigurePreScopeStart(WebApplication application)
    {
        application.UseResponseCaching();
        application.UseStaticFiles();
    }

    internal static void ConfigurePreAuthentication(WebApplication application, IAppSettings appSettings)
    {
        if (appSettings.Localization.Enabled)
            application.UseRequestLocalization();
    }

    internal static void ConfigurePostAuthorization(WebApplication application, DrnProgramSwaggerOptions options)
    {
        if (!options.AddSwagger) return;

        // Finalize document metadata and generator overrides before the UI captures its defaults.
        _ = application.Services.GetRequiredService<IOptions<SwaggerGenOptions>>().Value;
        var pageOptions = application.Services.GetRequiredService<IOptions<DrnPageOptions>>().Value;

        var policy = options.AuthorizationPolicy;
        if (policy is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(policy);

        var document = application.MapSwagger(options.DefaultRouteTemplate, options.ConfigureSwaggerEndpointOptions)
            .WithName(OpenApiDocumentEndpoints.DocumentEndpointName);
        if (policy is not null)
            document.RequireAuthorization(policy);

        if (options.AddSwaggerUI)
        {
            var defaultDocumentUrl = false;
            var ui = application.MapSwaggerUI(setupAction: uiOptions =>
            {
                options.ConfigureSwaggerUI(uiOptions, pageOptions);
                defaultDocumentUrl = OpenApiDocumentEndpoints.ConfigureSwaggerDocumentUrl(uiOptions, options);
            });
            if (policy is not null)
                ui.RequireAuthorization(policy);
            if (defaultDocumentUrl)
            {
                var redirect = application.MapGet(options.SwaggerUIPathPrefix!.Value.Add("/openapi-document.json").Value!,
                    (HttpContext context) => Results.LocalRedirect(OpenApiDocumentEndpoints.GetDocumentUrl(context, options)))
                    .ExcludeFromDescription();
                if (policy is not null)
                    redirect.RequireAuthorization(policy);
            }
        }
    }

    internal static void MapApplicationEndpoints(WebApplication application)
    {
        application.MapControllers();
        application.MapRazorPages();
    }
}
