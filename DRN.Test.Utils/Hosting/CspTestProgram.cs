using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using NetEscapades.AspNetCore.SecurityHeaders.Infrastructure;

namespace DRN.Test.Utils.Hosting;

public sealed class CspTestProgram : DrnProgramBase<CspTestProgram>, IDrnProgram
{
    public const string ReplaceSwaggerPolicyKey = "CspTest:ReplaceSwaggerPolicy";
    public const string SwaggerRoutePrefixKey = "CspTest:SwaggerRoutePrefix";
    public const string DisableSwaggerKey = "CspTest:DisableSwagger";

    public static async Task Main(string[] args) => await RunAsync(args);

    protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog)
    {
        builder.Services.AddAuthentication().AddCookie();
        return Task.CompletedTask;
    }

    protected override void ConfigureIdentityRenewal(IServiceCollection services, IAppSettings appSettings)
    {
        // This host tests CSP and does not use Identity cookie renewal.
    }

    protected override void ConfigureSwaggerOptions(DrnProgramSwaggerOptions options, IAppSettings appSettings)
    {
        base.ConfigureSwaggerOptions(options, appSettings);
        options.AddSwagger = !appSettings.Configuration.GetValue<bool>(DisableSwaggerKey);
        options.ConfigureSwaggerUIOptionsAction = ui =>
        {
            ui.RoutePrefix = appSettings.Configuration[SwaggerRoutePrefixKey] ?? ui.RoutePrefix;
            ui.DocumentTitle = "Custom API docs";
            ui.HeadContent = "<meta name=\"custom\" content=\"preserved\">";
            ui.CacheLifetime = TimeSpan.FromHours(1);
        };
    }

    protected override void ConfigureSecurityHeaderPolicyBuilder(
        SecurityHeaderPolicyBuilder builder, IServiceProvider serviceProvider, IAppSettings appSettings)
    {
        base.ConfigureSecurityHeaderPolicyBuilder(builder, serviceProvider, appSettings);
        if (!appSettings.Configuration.GetValue<bool>(ReplaceSwaggerPolicyKey)) return;

        var replacement = new HeaderPolicyCollection();
        ConfigureDefaultSecurityHeaders(replacement, serviceProvider, appSettings);
        replacement.Remove("Content-Security-Policy");
        replacement.AddContentSecurityPolicy(csp =>
        {
            ConfigureDefaultCspBase(csp);
            csp.AddScriptSrc().None();
        });
        builder.AddPolicy(CspFor.CspPolicySwagger, replacement);
    }

    protected override void MapApplicationEndpoints(WebApplication application, IAppSettings appSettings)
    {
        base.MapApplicationEndpoints(application, appSettings);
        // Anonymous metadata also lets Swagger middleware serve its document through the normal pipeline.
        application.MapGet("/{**path}", (HttpContext context) =>
        {
            if (context.Request.Query.TryGetValue("policy", out var policy))
                context.Items[CspFor.CspPolicyName] = policy.ToString();
            return Results.Content("<html></html>", "text/html");
        }).AllowAnonymous();
    }
}
