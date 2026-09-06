using DRN.Framework.Hosting.Middlewares;
using DRN.Framework.Hosting.Utils.Vite;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

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
        // Cache compressed bytes and include static files in both caching and compression.
        application.UseResponseCaching();
        application.UseResponseCompression();
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

        application.MapSwagger(options.DefaultRouteTemplate, options.ConfigureSwaggerEndpointOptions);
        application.UseSwaggerUI(options.ConfigureSwaggerUI);
    }

    internal static void MapApplicationEndpoints(WebApplication application)
    {
        application.MapControllers();
        application.MapRazorPages();
    }
}
