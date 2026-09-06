using System.Reflection;
using DRN.Framework.SharedKernel.Json;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Hosting.DrnProgram.Configurators;

internal static class DrnMvcConfigurator
{
    internal static void ConfigureMvcBuilder(IMvcBuilder mvcBuilder, Assembly programAssembly, string partName)
    {
        var applicationParts = mvcBuilder.PartManager.ApplicationParts;
        var controllersAdded = applicationParts.Any(p => p.Name == partName);
        if (!controllersAdded) mvcBuilder.AddApplicationPart(programAssembly);

        mvcBuilder.AddControllersAsServices();
        mvcBuilder.AddJsonOptions(options => JsonConventions.SetHtmlSafeWebJsonDefaults(options.JsonSerializerOptions));

        //learn.microsoft.com/en-us/aspnet/core/breaking-changes/10/razor-runtime-compilation-obsolete
        //learn.microsoft.com/en-us/aspnet/core/test/hot-reload
    }
}
