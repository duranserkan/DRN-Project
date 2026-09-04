using System.Runtime.ExceptionServices;
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Hosting.Extensions;
using DRN.Framework.Hosting.Utils.Vite;
using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace DRN.Framework.Testing.Contexts.Application;

internal sealed class DrnProgramHostRunner<TProgram>
    where TProgram : DrnProgramBase<TProgram>, IDrnProgram, new()
{
    public IHost BuildHost(
        DrnTestContext testContext,
        List<Action<IConfigurationBuilder>> hostConfigs,
        List<Action<HostBuilderContext, IConfigurationBuilder>> appConfigs,
        List<Action<HostBuilderContext, IServiceCollection>> servicesConfigs,
        IDictionary<object, object> properties)
    {
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddDrnSettings(typeof(TProgram).GetAssemblyName(), []);
        configBuilder.AddConfiguration(testContext.BuildConfigurationRoot());

        foreach (var hostConfig in hostConfigs)
            hostConfig(configBuilder);

        var webRootPath = ApplicationContextHelper.ResolveWebRootPath(typeof(TProgram));
        var hasWebRoot = !string.IsNullOrEmpty(webRootPath);

        var initialConfiguration = configBuilder.Build();
        var hostContext = new HostBuilderContext(properties)
        {
            Configuration = initialConfiguration,
            HostingEnvironment = new HostEnvironment
            {
                ApplicationName = typeof(TProgram).Assembly.GetName().Name ?? string.Empty,
                EnvironmentName = initialConfiguration["Environment"] ?? nameof(AppEnvironment.Development),
                ContentRootPath = AppContext.BaseDirectory,
                WebRootPath = webRootPath
            }
        };

        foreach (var appConfig in appConfigs)
            appConfig(hostContext, configBuilder);

        var configuration = configBuilder.Build();
        var appSettings = new AppSettings(configuration);

        try
        {
            var scopedLog = new ScopedLog(appSettings);
            var application = DrnProgramBase<TProgram>.CreateApplicationAsync(
                args: [],
                appSettings: appSettings,
                scopeLog: scopedLog,
                configureBuilder: applicationBuilder =>
                {
                    if (hasWebRoot)
                    {
                        applicationBuilder.Environment.WebRootPath = webRootPath;
                        applicationBuilder.Environment.WebRootFileProvider = new PhysicalFileProvider(webRootPath);
                        applicationBuilder.WebHost.UseWebRoot(webRootPath);
                    }
                    else
                    {
                        applicationBuilder.Services.AddSingleton<IViteManifest, EmptyViteManifest>();
                    }

                    hostContext.HostingEnvironment = applicationBuilder.Environment;
                    hostContext.Configuration = applicationBuilder.Configuration;

                    applicationBuilder.Configuration.AddConfiguration(configuration);
                    applicationBuilder.WebHost.UseTestServer();

                    foreach (var configServices in servicesConfigs)
                        configServices(hostContext, applicationBuilder.Services);
                }).GetAwaiter().GetResult();

            return new AppSettingsOwnedHost(application, appSettings);
        }
        catch (Exception hostException)
        {
            try
            {
                appSettings.Dispose();
            }
            catch (Exception disposalException)
            {
                throw new AggregateException(hostException, disposalException);
            }

            throw;
        }
    }

    private sealed class AppSettingsOwnedHost(IHost host, AppSettings appSettings) : IHost
    {
        private bool _hostDisposed;
        private bool _appSettingsDisposed;

        public IServiceProvider Services => host.Services;

        public Task StartAsync(CancellationToken cancellationToken = default) => host.StartAsync(cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken = default) => host.StopAsync(cancellationToken);

        public void Dispose()
        {
            Exception? hostException = null;
            Exception? appSettingsException = null;

            if (!_hostDisposed)
            {
                try
                {
                    host.Dispose();
                    _hostDisposed = true;
                }
                catch (Exception exception)
                {
                    hostException = exception;
                }
            }

            if (!_appSettingsDisposed)
            {
                try
                {
                    appSettings.Dispose();
                    _appSettingsDisposed = true;
                }
                catch (Exception exception)
                {
                    appSettingsException = exception;
                }
            }

            if (hostException is not null && appSettingsException is not null)
                throw new AggregateException(hostException, appSettingsException);

            if (hostException is not null)
                ExceptionDispatchInfo.Capture(hostException).Throw();

            if (appSettingsException is not null)
                ExceptionDispatchInfo.Capture(appSettingsException).Throw();
        }
    }

    private sealed class HostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = string.Empty;
        public string ApplicationName { get; set; } = string.Empty;
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
