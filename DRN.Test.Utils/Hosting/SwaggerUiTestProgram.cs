using DRN.Framework.Hosting;
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using DRN.Test.Utils.Hosting.Auth;
using Microsoft.AspNetCore.Authentication;

namespace DRN.Test.Utils.Hosting;

public sealed class SwaggerUiTestProgram : DrnProgramBase<SwaggerUiTestProgram>, IDrnProgram
{
    public const string UiEnabledKey = "SwaggerUiTest:UiEnabled";
    public const string NamedPolicyKey = "SwaggerUiTest:NamedPolicy";
    public static async Task Main(string[] args) => await RunAsync(args);

    protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog)
    {
        builder.Services.Configure<DrnPageOptions>(options => options.FaviconUrl = "/images/icon.svg");
        builder.Services.AddAuthentication(NonDefaultExemptValues.DefaultCookieScheme)
            .AddScheme<AuthenticationSchemeOptions, NonDefaultCookieAuthHandler>(NonDefaultExemptValues.DefaultCookieScheme, _ => { });
        builder.Services.AddAuthorizationBuilder().AddPolicy("SwaggerUiTestPolicy",
            policy => policy.RequireRole(NonDefaultExemptValues.ManagerRole));
        return Task.CompletedTask;
    }

    protected override void ConfigureIdentityRenewal(IServiceCollection services, IAppSettings appSettings)
    {
        // This fixture uses a test authentication handler rather than Identity cookies.
    }

    protected override void ConfigureSwaggerOptions(DrnProgramSwaggerOptions options, IAppSettings appSettings)
    {
        base.ConfigureSwaggerOptions(options, appSettings);
        options.AddSwagger = true;
        options.AddSwaggerUI = appSettings.Configuration.GetValue(UiEnabledKey, true);
        options.AuthorizationPolicy = appSettings.Configuration.GetValue(NamedPolicyKey, false) ? "SwaggerUiTestPolicy" : null;
        options.ConfigureOpenApiInfo = info => { info.Title = "Configured API"; info.Version = "v2"; };
        options.ConfigureSwaggerGenOptionsAction = generator => generator.SwaggerGeneratorOptions.SwaggerDocs["v2"].Title = "Generator API";
        options.ConfigureSwaggerUIOptionsAction = ui => ui.RoutePrefix = "docs/swagger";
        options.DefaultRouteTemplate = "/specs/{documentName}/openapi.{extension:regex(^(json|ya?ml)$)}";
    }

    protected override void ConfigureApplicationPipelineStart(WebApplication application, IAppSettings appSettings)
    {
        application.UsePathBase("/gateway");
        base.ConfigureApplicationPipelineStart(application, appSettings);
    }
}
