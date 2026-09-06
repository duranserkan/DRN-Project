using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Utils.DependencyInjection;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;

namespace DRN.Test.Utils.Hosting;

public sealed class ForwardedHeadersTestProgram : DrnProgramBase<ForwardedHeadersTestProgram>, IDrnProgram
{
    public const string ResourcePath = "/forwarded-headers/resource";

    public static async Task Main(string[] args) => await RunAsync(args);

    protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog)
    {
        builder.Services.AddAuthentication().AddCookie();
        builder.Services.AddServicesWithAttributes();
        return Task.CompletedTask;
    }

    protected override void ConfigureSwaggerOptions(DrnProgramSwaggerOptions options, IAppSettings appSettings)
    {
        base.ConfigureSwaggerOptions(options, appSettings);
        options.AddSwagger = false;
    }

    protected override void ConfigureIdentityRenewal(IServiceCollection services, IAppSettings appSettings)
    {
        // This test host does not use Identity cookies or their renewal services.
    }

    protected override void MapApplicationEndpoints(WebApplication application, IAppSettings appSettings)
    {
        base.MapApplicationEndpoints(application, appSettings);
        application.MapGet(ResourcePath, (HttpContext context) => new ForwardedHeadersResource(
                "test-resource",
                context.Request.Scheme,
                context.Request.IsHttps,
                context.Request.Host.Value ?? string.Empty,
                context.Request.PathBase.Value ?? string.Empty,
                context.Request.Path.Value ?? string.Empty,
                context.Connection.RemoteIpAddress?.ToString()))
            .AllowAnonymous();
    }
}

public sealed record ForwardedHeadersResource(
    string Content, string Scheme, bool IsHttps, string Host, string PathBase, string Path, string? RemoteIpAddress);
