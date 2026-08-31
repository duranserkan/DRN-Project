using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Hosting.Utils.Vite;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Test.Utils.Hosting;

public sealed class SecondaryWebRootProgram : DrnProgramBase<SecondaryWebRootProgram>, IDrnProgram
{
    public static string? ResolvedWebRootPath { get; private set; }
    public static IViteManifest? ResolvedViteManifest { get; private set; }

    public static async Task Main(string[] args) => await RunAsync(args);

    public static void Reset()
    {
        ResolvedWebRootPath = null;
        ResolvedViteManifest = null;
    }

    protected override void ConfigureApplication(WebApplication application, IAppSettings appSettings)
    {
        var env = application.Services.GetRequiredService<IWebHostEnvironment>();
        ResolvedWebRootPath = env.WebRootPath;
        ResolvedViteManifest = application.Services.GetService<IViteManifest>();
        application.UseStaticFiles();
    }

    protected override void ValidateEndpoints(WebApplication application, IAppSettings appSettings) { }

    protected override Task ValidateServicesAsync(WebApplication application, IScopedLog scopeLog) => Task.CompletedTask;

    protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog) => Task.CompletedTask;
}
