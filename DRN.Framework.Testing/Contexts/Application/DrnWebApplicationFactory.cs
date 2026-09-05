using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Hosting.Utils.Vite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DRN.Framework.Testing.Contexts.Application;

public class DrnWebApplicationFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : DrnProgramBase<TEntryPoint>, IDrnProgram, new()
{
    private readonly DrnTestContext _context;
    private readonly Action<IWebHostBuilder>? _webHostConfigurator;

    public DrnWebApplicationFactory(DrnTestContext context, bool temporary = false)
        : this(context, temporary, null)
    {
    }

    internal DrnWebApplicationFactory(
        DrnTestContext context,
        bool temporary,
        Action<IWebHostBuilder>? webHostConfigurator)
    {
        _context = context;
        Temporary = temporary;
        _webHostConfigurator = webHostConfigurator;
    }

    private bool Temporary { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var webRootPath = ApplicationContextHelper.ResolveWebRootPath(typeof(TEntryPoint));
        if (string.IsNullOrEmpty(webRootPath))
            builder.ConfigureServices(services => services.AddSingleton<IViteManifest, EmptyViteManifest>());
        else
            builder.UseWebRoot(webRootPath);

        _webHostConfigurator?.Invoke(builder);
    }

    protected override IHostBuilder? CreateHostBuilder()
    {
        var entryPointType = typeof(TEntryPoint);
        var defaultEntryPoint = entryPointType.Assembly.EntryPoint?.DeclaringType;

        if (defaultEntryPoint != entryPointType)
            return new DrnProgramHostBuilder<TEntryPoint>(_context);

        return base.CreateHostBuilder();
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Preserve WebApplicationFactory host setup while retaining a partially built host for failure cleanup.
        var capturingBuilder = new CapturingHostBuilder(builder);
        try
        {
            var host = base.CreateHost(capturingBuilder);
            if (Temporary)
                return new FailureSafeHost(host);

            _context.UseApplicationServiceProvider(host.Services);
            if (host.Services.GetService(typeof(IConfiguration)) is IConfiguration configuration)
                _context.ApplicationContext.RegisterConfiguredAddresses(typeof(TEntryPoint), configuration);

            return new FailureSafeHost(host);
        }
        catch (Exception hostException)
        {
            var host = capturingBuilder.Host;
            if (host == null)
                throw;

            try
            {
                host.Dispose();
            }
            catch (Exception disposalException)
            {
                throw new AggregateException(hostException, disposalException);
            }

            throw;
        }
    }

    private sealed class DrnProgramHostBuilder<TProgram>(
        DrnTestContext context) : IHostBuilder
        where TProgram : DrnProgramBase<TProgram>, IDrnProgram, new()
    {
        private readonly List<Action<IConfigurationBuilder>> _configureHostConfigs = [];
        private readonly List<Action<HostBuilderContext, IServiceCollection>> _configureServices = [];
        private readonly List<Action<HostBuilderContext, IConfigurationBuilder>> _configureAppConfigs = [];
        private readonly List<Action<WebApplicationBuilder, HostBuilderContext>> _configureContainerActions = [];

        public IDictionary<object, object> Properties { get; } = new Dictionary<object, object>();

        public IHostBuilder ConfigureHostConfiguration(Action<IConfigurationBuilder> configureDelegate)
        {
            _configureHostConfigs.Add(configureDelegate);
            return this;
        }

        public IHostBuilder ConfigureAppConfiguration(Action<HostBuilderContext, IConfigurationBuilder> configureDelegate)
        {
            _configureAppConfigs.Add(configureDelegate);
            return this;
        }

        public IHostBuilder ConfigureServices(Action<HostBuilderContext, IServiceCollection> configureDelegate)
        {
            _configureServices.Add(configureDelegate);
            return this;
        }

        public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(
            IServiceProviderFactory<TContainerBuilder> factory) where TContainerBuilder : notnull
        {
            ArgumentNullException.ThrowIfNull(factory);
            _configureContainerActions.Add((builder, _) => builder.Host.UseServiceProviderFactory(factory));
            return this;
        }

        public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(
            Func<HostBuilderContext, IServiceProviderFactory<TContainerBuilder>> factory)
            where TContainerBuilder : notnull
        {
            ArgumentNullException.ThrowIfNull(factory);
            _configureContainerActions.Add((builder, hostContext) => builder.Host.UseServiceProviderFactory(factory(hostContext)));
            return this;
        }

        public IHostBuilder ConfigureContainer<TContainerBuilder>(
            Action<HostBuilderContext, TContainerBuilder> configureDelegate)
        {
            ArgumentNullException.ThrowIfNull(configureDelegate);
            _configureContainerActions.Add((builder, _) => builder.Host.ConfigureContainer(configureDelegate));
            return this;
        }

        public IHost Build()
        {
            var runner = new DrnProgramHostRunner<TProgram>();
            return runner.BuildHost(
                context,
                _configureHostConfigs,
                _configureAppConfigs,
                _configureServices,
                _configureContainerActions,
                Properties);
        }
    }

    private sealed class FailureSafeHost(IHost host) : IHost
    {
        private bool _disposedAfterStopFailure;
        private bool _stopped;

        public IServiceProvider Services => host.Services;

        public Task StartAsync(CancellationToken cancellationToken = default) =>
            host.StartAsync(cancellationToken);

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (_disposedAfterStopFailure || _stopped)
                return;

            _stopped = true;
            try
            {
                await host.StopAsync(cancellationToken);
            }
            catch (Exception stopException)
            {
                try
                {
                    host.Dispose();
                    _disposedAfterStopFailure = true;
                }
                catch (Exception disposalException)
                {
                    throw new AggregateException(stopException, disposalException);
                }

                throw;
            }
        }

        public void Dispose()
        {
            if (!_disposedAfterStopFailure)
                host.Dispose();
        }
    }

    private sealed class CapturingHostBuilder(IHostBuilder builder) : IHostBuilder
    {
        public IHost? Host { get; private set; }
        public IDictionary<object, object> Properties => builder.Properties;

        public IHostBuilder ConfigureHostConfiguration(Action<IConfigurationBuilder> configureDelegate)
        {
            builder.ConfigureHostConfiguration(configureDelegate);
            return this;
        }

        public IHostBuilder ConfigureAppConfiguration(
            Action<HostBuilderContext, IConfigurationBuilder> configureDelegate)
        {
            builder.ConfigureAppConfiguration(configureDelegate);
            return this;
        }

        public IHostBuilder ConfigureServices(Action<HostBuilderContext, IServiceCollection> configureDelegate)
        {
            builder.ConfigureServices(configureDelegate);
            return this;
        }

        public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(
            IServiceProviderFactory<TContainerBuilder> factory) where TContainerBuilder : notnull
        {
            builder.UseServiceProviderFactory(factory);
            return this;
        }

        public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(
            Func<HostBuilderContext, IServiceProviderFactory<TContainerBuilder>> factory)
            where TContainerBuilder : notnull
        {
            builder.UseServiceProviderFactory(factory);
            return this;
        }

        public IHostBuilder ConfigureContainer<TContainerBuilder>(
            Action<HostBuilderContext, TContainerBuilder> configureDelegate)
        {
            builder.ConfigureContainer(configureDelegate);
            return this;
        }

        public IHost Build() => Host = builder.Build();
    }
}
