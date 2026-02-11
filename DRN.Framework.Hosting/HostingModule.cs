using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.SharedKernel.Json;
using DRN.Framework.Utils.DependencyInjection;
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
        sc.ConfigureHttpJsonOptions(jsonOptions => JsonConventions.SetJsonDefaults(jsonOptions.SerializerOptions));
        sc.AddLogging();
        sc.AddEndpointsApiExplorer();

        sc.AddServicesWithAttributes();
        if (options.AddSwagger)
            sc.AddSwaggerGen(options.ConfigureSwaggerGenOptions);

        return sc;
    }
}