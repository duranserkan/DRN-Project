using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace DRN.Framework.Hosting.DrnProgram.Configurators;

internal static class DrnWebHostConfigurator
{
    internal static IWebHostBuilder ConfigureWebHostBuilder(IAppSettings appSettings, ConfigureWebHostBuilder webHostBuilder) =>
        webHostBuilder.UseKestrel().ConfigureKestrel(kestrelServerOptions =>
        {
            kestrelServerOptions.AddServerHeader = false;
            if (appSettings.TryGetSection("Kestrel", out var kestrelSection))
                kestrelServerOptions.Configure(kestrelSection);
        }).UseStaticWebAssets();
}
