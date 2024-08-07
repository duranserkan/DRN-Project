using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Utils.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Hosting;

public static class HostingModule
{
    public static IServiceCollection AdDrnHosting(this IServiceCollection sc, DrnProgramSwaggerOptions options)
    {
        sc.AddServicesWithAttributes();
        sc.AddSwaggerGen(options.ConfigureSwaggerGenOptions);

        return sc;
    }
}