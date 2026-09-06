using DRN.Framework.Utils.Settings;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using NLog.Web;

namespace DRN.Framework.Hosting.DrnProgram.Configurators;

internal static class DrnNLogConfigurator
{
    internal static NLogAspNetCoreOptions CreateDefaultOptions() => new()
    {
        ReplaceLoggerFactory = false,
        RemoveLoggerFactoryFilter = false
    };

    internal static LogFactory CreateLogFactory(IAppSettings appSettings, string configSectionName)
    {
        var logFactory = new LogFactory();
        logFactory.Setup().SetupExtensions(ext =>
        {
            ext.RegisterAssembly("NLog.Extensions.Logging");
            ext.RegisterAssembly("NLog.Web.AspNetCore");
            ext.RegisterAssembly("NLog.Targets.Network");
        });

        var configuration = new NLogLoggingConfiguration(appSettings.GetRequiredSection(configSectionName));
        logFactory.Configuration = configuration;

        return logFactory;
    }

    internal static void ConfigureLoggingBuilder(
        IAppSettings appSettings,
        ILoggingBuilder loggingBuilder,
        string configSectionName,
        NLogAspNetCoreOptions options)
    {
        if (appSettings.TryGetSection("Logging", out var loggingSection))
            loggingBuilder.AddConfiguration(loggingSection);

        loggingBuilder.ClearProviders();
        if (appSettings.TryGetSection(configSectionName, out _))
            loggingBuilder.AddNLogWeb(CreateLogFactory(appSettings, configSectionName), options);
    }
}
