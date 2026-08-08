using System.Net.Http.Json;
using DRN.Framework.Utils.Models.Sample;
using DRN.Test.Utils.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Sample.Hosted;
using Sample.Hosted.Filters;
using Sample.Hosted.Helpers;
using Sample.Infra.QA;

namespace DRN.Test.Integration.Tests.Framework.Testing;

public class ApplicationContextTests
{
    [Fact]
    public void ApplicationContext_Should_Resolve_Active_Xunit_Output_Helper()
    {
        var ambientOutputHelper = Xunit.TestContext.Current.TestOutputHelper;

        ambientOutputHelper.Should().NotBeNull();
        InvokeOutputHelperResolver().Should().BeSameAs(ambientOutputHelper);
    }

    [Fact]
    public void ApplicationContext_Should_Prefer_Explicit_Output_Helper()
    {
        var suppliedOutputHelper = Substitute.For<ITestOutputHelper>();

        InvokeOutputHelperResolver(suppliedOutputHelper).Should().BeSameAs(suppliedOutputHelper);
    }

    [Fact]
    public async Task ApplicationContext_Should_Allow_Missing_Output_Helper_Outside_Active_Test_Context()
    {
        Task<ITestOutputHelper?> resolutionTask;
        using (ExecutionContext.SuppressFlow())
            resolutionTask = Task.Run(() => InvokeOutputHelperResolver());

        var resolvedOutputHelper = await resolutionTask;
        resolvedOutputHelper.Should().BeNull();
    }

    [Theory]
    [DataInline]
    public void ApplicationContext_Should_Not_Reuse_Output_Helper_Between_Sequential_Applications(
        DrnTestContext context)
    {
        const string firstApplicationMessage = "first application output helper";
        const string secondApplicationMessage = "second application without output helper";
        var outputHelper = Substitute.For<ITestOutputHelper>();

        var firstApplication =
            InvokeCreateApplicationCore<TemporaryLifecycleProgram>(context.ApplicationContext, outputHelper);
        _ = firstApplication.Server;
        firstApplication.Services.GetRequiredService<ILogger<ApplicationContextTests>>()
            .LogCritical(firstApplicationMessage);

        outputHelper.Received().WriteLine(
            Arg.Is<string>(message => message != null && message.Contains(firstApplicationMessage, StringComparison.Ordinal)));

        var secondApplication =
            InvokeCreateApplicationCore<TemporaryLifecycleProgram>(context.ApplicationContext, null);
        _ = secondApplication.Server;
        secondApplication.Services.GetRequiredService<ILogger<ApplicationContextTests>>()
            .LogCritical(secondApplicationMessage);

        outputHelper.DidNotReceive().WriteLine(
            Arg.Is<string>(message => message != null && message.Contains(secondApplicationMessage, StringComparison.Ordinal)));
    }

    [Theory]
    [DataInline]
    public async Task ApplicationContext_Should_Isolate_Sequential_Applications(DrnTestContext context)
    {
        const string configurationKey = "ApplicationContextTests:SequentialValue";
        context.AddToConfiguration(configurationKey, "first");

        var firstApplication = await context.ApplicationContext.CreateApplicationAndBindDependenciesAsync<SampleProgram>();
        using var firstClient = firstApplication.CreateClient();
        firstApplication.Services.GetRequiredService<ISampleDrnExceptionFilterDependency>().Should().NotBeNull();
        firstApplication.Services.GetRequiredService<IConfiguration>()[configurationKey].Should().Be("first");

        context.AddToConfiguration(configurationKey, "second");
        var secondApplication = context.ApplicationContext.CreateApplication<TemporaryLifecycleProgram>();
        using var secondClient = secondApplication.CreateClient();

        secondClient.Should().NotBeSameAs(firstClient);
        secondApplication.Services.GetService<ISampleDrnExceptionFilterDependency>().Should().BeNull();
        secondApplication.Services.GetRequiredService<IConfiguration>()[configurationKey].Should().Be("second");
    }

    [Theory]
    [DataInline]
    public void ApplicationContext_Should_Preserve_Caller_Service_And_Logging_Overrides(DrnTestContext context)
    {
        var contextRegistration = new HostConfiguratorRegistration("context");
        var callerRegistration = new HostConfiguratorRegistration("caller");
        var callerLoggingProvider = new CallerLoggingProvider();
        context.ServiceCollection.AddSingleton(contextRegistration);

        var application = context.ApplicationContext.CreateApplication<TemporaryLifecycleProgram>(builder =>
        {
            builder.ConfigureServices(services => services.AddSingleton(callerRegistration));
            builder.ConfigureLogging(logging => logging.AddProvider(callerLoggingProvider));
        });
        _ = application.Server;

        application.Services.GetRequiredService<HostConfiguratorRegistration>().Should().BeSameAs(callerRegistration);
        application.Services.GetServices<ILoggerProvider>().Should().Contain(callerLoggingProvider);
    }

    [Theory]
    [DataInline]
    public void DrnTestContext_Should_Retry_Factory_Before_Disposing_Owned_ServiceProvider(DrnTestContext context)
    {
        var disposalOrder = new List<string>();
        context.ServiceCollection.AddSingleton(_ => new ContextOwnedDisposalTracker(disposalOrder));
        _ = context.GetRequiredService<ContextOwnedDisposalTracker>();

        var hostedService = new StopFailureHostedService(disposalOrder);
        var application = context.ApplicationContext.CreateApplication<TemporaryLifecycleProgram>(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IHostedService>(hostedService);
                services.AddSingleton(_ => new ApplicationOwnedDisposalTracker(disposalOrder));
            }));
        _ = application.Server;
        _ = application.Services.GetRequiredService<ApplicationOwnedDisposalTracker>();
        hostedService.FailNextStop();

        var dispose = () => context.Dispose();

        var exception = dispose.Should().Throw<AggregateException>().Which;
        exception.ToString().Should().Contain(StopFailureHostedService.StopFailureMessage);
        context.ApplicationContext.GetCreatedApplication<TemporaryLifecycleProgram>().Should().BeNull();
        disposalOrder.Should().Equal("application-stop-failed", "application", "context");

        context.Dispose();

        disposalOrder.Should().HaveCount(3);
    }

    [Theory]
    [DataInline]
    public void ApplicationContext_Should_Dispose_Host_When_Stop_Fails(DrnTestContext context)
    {
        var disposalOrder = new List<string>();
        var hostedService = new StopFailureHostedService(disposalOrder);
        var application = context.ApplicationContext.CreateApplication<TemporaryLifecycleProgram>(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IHostedService>(hostedService);
                services.AddSingleton(_ => new ApplicationOwnedDisposalTracker(disposalOrder));
            }));
        _ = application.Server;
        _ = application.Services.GetRequiredService<ApplicationOwnedDisposalTracker>();
        hostedService.FailNextStop();

        var dispose = () => context.ApplicationContext.Dispose();

        var exception = dispose.Should().Throw<Exception>().Which;
        exception.ToString().Should().Contain(StopFailureHostedService.StopFailureMessage);
        disposalOrder.Should().Equal("application-stop-failed", "application");
        context.ApplicationContext.HasCreatedApplication.Should().BeTrue();

        context.ApplicationContext.Dispose();

        context.ApplicationContext.HasCreatedApplication.Should().BeFalse();
        disposalOrder.Should().HaveCount(2);
    }

    [Theory]
    [DataInline]
    public void ApplicationContext_Should_Retry_Parent_After_Derived_Disposal_Fails(DrnTestContext context)
    {
        var disposalOrder = new List<string>();
        // ApplicationContext owns only the parent factory. Model nested cleanup explicitly so this test covers
        // its retry contract without depending on WebApplicationFactory's internal derived-factory traversal.
        var factory = new ParentApplicationFactoryWithDerivedDisposalFailure(disposalOrder);
        context.ApplicationContext.UseApplicationFactory(factory);

        var firstDispose = () => context.ApplicationContext.Dispose();
        var exception = firstDispose.Should().Throw<Exception>().Which;
        exception.ToString().Should().Contain(DerivedApplicationFactoryWithOneShotDisposalFailure.FailureMessage);
        disposalOrder.Should().Equal("derived-application-dispose-failed", "derived-application");
        context.ApplicationContext.HasCreatedApplication.Should().BeTrue();

        context.ApplicationContext.Dispose();

        context.ApplicationContext.HasCreatedApplication.Should().BeFalse();
        disposalOrder.Should().Equal(
            "derived-application-dispose-failed", "derived-application", "parent-application");
    }

    [Theory]
    [DataInline]
    public void ApplicationContext_Should_Dispose_Derived_Factory_Before_Parent(DrnTestContext context)
    {
        var disposalOrder = new List<string>();
        var derivedHostedService = new StopTrackingHostedService(disposalOrder);
        var application = context.ApplicationContext.CreateApplication<TemporaryLifecycleProgram>(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton(_ => new ParentApplicationDisposalTracker(disposalOrder))));
        _ = application.Server;
        _ = application.Services.GetRequiredService<ParentApplicationDisposalTracker>();

        var derivedApplication = application.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IHostedService>(derivedHostedService);
                services.AddSingleton(_ => new ApplicationOwnedDisposalTracker(disposalOrder));
            }));
        _ = derivedApplication.Server;
        _ = derivedApplication.Services.GetRequiredService<ApplicationOwnedDisposalTracker>();
        application.Factories.Should().ContainSingle().Which.Should().BeSameAs(derivedApplication);

        context.ApplicationContext.Dispose();

        context.ApplicationContext.HasCreatedApplication.Should().BeFalse();
        disposalOrder.Should().Equal("derived-application-stop", "application", "parent-application");
    }

    [Theory]
    [DataInline]
    public void DrnTestContext_Should_Capture_Both_Factory_Failures_Before_Remaining_Cleanup(DrnTestContext context)
    {
        var disposalOrder = new List<string>();
        context.ServiceCollection.AddSingleton(_ => new ContextOwnedDisposalTracker(disposalOrder));
        _ = context.GetRequiredService<ContextOwnedDisposalTracker>();

        var factory = new RepeatedApplicationFactoryDisposeFailure(disposalOrder);
        context.ApplicationContext.UseApplicationFactory(factory);

        var dispose = () => context.Dispose();

        var exception = dispose.Should().Throw<AggregateException>().Which;
        var errors = exception.Flatten().InnerExceptions;
        errors.Should().HaveCount(2);
        errors[0].Message.Should().Contain("Attempt 1");
        errors[1].Message.Should().Contain("Attempt 2");
        factory.DisposeCount.Should().Be(2);
        context.ApplicationContext.HasCreatedApplication.Should().BeTrue();
        disposalOrder.Should().Equal("application-dispose-failed-1", "application-dispose-failed-2", "context");

        context.Dispose();

        disposalOrder.Should().HaveCount(3);
    }

    [Theory]
    [DataInline]
    public void ApplicationContext_Should_Rebuild_After_Owned_Provider_Disposal_Failure(DrnTestContext context)
    {
        context.ServiceCollection.AddSingleton<OneShotDisposeFailure>();
        var throwingDisposable = context.GetRequiredService<OneShotDisposeFailure>();
        var firstApplication = context.ApplicationContext.CreateApplication<TemporaryLifecycleProgram>();
        _ = firstApplication.Server;

        var firstDispose = () => context.ApplicationContext.Dispose();

        var exception = firstDispose.Should().Throw<Exception>().Which;
        exception.ToString().Should().Contain(OneShotDisposeFailure.FailureMessage);
        throwingDisposable.DisposeCount.Should().Be(1);

        var secondApplication = context.ApplicationContext.CreateApplication<TemporaryLifecycleProgram>();
        _ = secondApplication.Server;

        context.GetRequiredService<IConfiguration>().Should().BeSameAs(
            secondApplication.Services.GetRequiredService<IConfiguration>());
    }

    [Theory]
    [DataInline]
    public void DrnTestContext_Should_Dispose_Application_Before_Owned_ServiceProvider(DrnTestContext context)
    {
        var disposalOrder = new List<string>();
        context.ServiceCollection.AddSingleton(_ => new ContextOwnedDisposalTracker(disposalOrder));
        _ = context.GetRequiredService<ContextOwnedDisposalTracker>();

        var application = context.ApplicationContext.CreateApplication<TemporaryLifecycleProgram>(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton(_ => new ApplicationOwnedDisposalTracker(disposalOrder))));
        _ = application.Services.GetRequiredService<ApplicationOwnedDisposalTracker>();

        context.Dispose();

        disposalOrder.Should().Equal("application", "context");
    }

    [Theory]
    [DataInline]
    public void ApplicationContext_Should_Dispose_Temporary_Discovery_Factory_When_Host_Fails(DrnTestContext context)
    {
        var throwingOptions = new ThrowingTestServerOptions();

        Action createApplication = () => context.ApplicationContext.CreateApplication<TemporaryLifecycleProgram>(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<IOptions<TestServerOptions>>(_ => throwingOptions)));

        createApplication.Should().ThrowExactly<InvalidOperationException>()
            .WithMessage(ThrowingTestServerOptions.FailureMessage);
        throwingOptions.DisposeCount.Should().Be(1);
        context.ApplicationContext.HasCreatedApplication.Should().BeFalse();
    }

    [Theory]
    [DataInline]
    public void DrnWebApplicationFactory_Should_Preserve_Base_Kestrel_Port_Configuration(DrnTestContext context)
    {
        var addressesFeature = new ServerAddressesFeature();
        addressesFeature.Addresses.Add("http://localhost:5000");
        var features = new FeatureCollection();
        features.Set<IServerAddressesFeature>(addressesFeature);
        var server = Substitute.For<IServer>();
        server.Features.Returns(features);
        var builder = new HostBuilder().ConfigureServices(services => services.AddSingleton(server));
        using var factory = new ExposedDrnWebApplicationFactory(context);
        factory.UseKestrel(0);

        using var host = factory.CreateHostForTest(builder);

        addressesFeature.Addresses.Should().Equal("http://127.0.0.1:0");
    }

    [Theory]
    [DataInline]
    public async Task DrnWebApplicationFactory_Host_Disposal_Should_Preserve_Stop_And_Disposal_Failures(
        DrnTestContext context)
    {
        const string stopFailureMessage = "Host stop failure.";
        const string disposalFailureMessage = "Host disposal failure";
        var server = Substitute.For<IServer>();
        server.Features.Returns(new FeatureCollection());
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IServer)).Returns(server);

        var host = Substitute.For<IHost>();
        host.Services.Returns(serviceProvider);
        host.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        host.StopAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException(stopFailureMessage)));

        var disposalAttempts = 0;
        host.When(candidate => candidate.Dispose()).Do(_ =>
        {
            disposalAttempts++;
            if (disposalAttempts <= 2)
                throw new InvalidOperationException($"{disposalFailureMessage} {disposalAttempts}.");
        });

        var hostBuilder = Substitute.For<IHostBuilder>();
        hostBuilder.Properties.Returns(new Dictionary<object, object>());
        hostBuilder.Build().Returns(host);
        using var factory = new ExposedDrnWebApplicationFactory(context);
        factory.UseKestrel(0);
        var failureSafeHost = factory.CreateHostForTest(hostBuilder);
        Func<Task> stop = () => failureSafeHost.StopAsync();
        Action dispose = failureSafeHost.Dispose;

        var firstException = (await stop.Should().ThrowAsync<AggregateException>()).Which;

        firstException.Flatten().InnerExceptions.Should().HaveCount(2);
        firstException.ToString().Should().Contain(stopFailureMessage).And.Contain($"{disposalFailureMessage} 1.");
        disposalAttempts.Should().Be(1);

        await stop();
        disposalAttempts.Should().Be(1);

        var retryException = dispose.Should().Throw<InvalidOperationException>().Which;
        retryException.Message.Should().Be($"{disposalFailureMessage} 2.");
        disposalAttempts.Should().Be(2);

        dispose();
        disposalAttempts.Should().Be(3);
    }

    [Theory]
    [DataInline]
    public void ApplicationContext_Hosts_Should_Not_Emit_Lifecycle_Logs(DrnTestContext context)
    {
        TemporaryLifecycleProgram.Reset();

        var application = context.ApplicationContext.CreateApplication<TemporaryLifecycleProgram>();
        _ = application.Server;

        TemporaryLifecycleProgram.CapturedLifecycleLogCount.Should().Be(0);
    }

    [Theory]
    [DataInline]
    public async Task ApplicationContext_Should_Provide_Configuration_To_Program(DrnTestContext context)
    {
        var webApplication = context.ApplicationContext.CreateApplication<SampleProgram>();
        await context.ContainerContext.Postgres.ApplyMigrationsAsync();
        context.HasOwnedServiceProvider.Should().BeFalse();

        var client = webApplication.CreateClient();
        var endpoint = Get.Endpoint.Sample.WeatherForecast.Get.RoutePattern;
        var forecasts = await client.GetFromJsonAsync<WeatherForecast[]>(endpoint);
        forecasts.Should().NotBeNull();

        var appSettingsFromWebApplication = webApplication.Services.GetRequiredService<IAppSettings>();
        var connectionString = appSettingsFromWebApplication.GetRequiredConnectionString(nameof(QAContext));
        connectionString.Should().NotBeNull();
        new NpgsqlConnectionStringBuilder(connectionString).Pooling.Should().BeFalse();

        var appSettingsFromDrnTestContext = context.GetRequiredService<IAppSettings>();
        appSettingsFromWebApplication.Should().BeSameAs(appSettingsFromDrnTestContext); //resolved from same service provider

        //comes from settings.json in test project's global data directory
        var duckTest = "If it looks like a duck, swims like a duck, and quacks like a duck, then it probably is a duck";
        appSettingsFromDrnTestContext.GetValue("DuckTest", "").Should().Be(duckTest);

        //comes from appsettings.json in web application's directory
        var saganStandard = "Extraordinary claims require extraordinary evidence";
        appSettingsFromDrnTestContext.GetValue("SaganStandard", "").Should().Be(saganStandard);

        //appsettings.json value is overriden by settings.json
        var philosophicalRazor = "Never attribute to malice that which can be adequately explained by incompetence or stupidity";
        appSettingsFromDrnTestContext.GetValue("PhilosophicalRazor", "").Should().Be(philosophicalRazor);
    }

    private sealed class ContextOwnedDisposalTracker(List<string> disposalOrder) : IDisposable
    {
        public void Dispose() => disposalOrder.Add("context");
    }

    private sealed record HostConfiguratorRegistration(string Source);

    private sealed class CallerLoggingProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) =>
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        public void Dispose()
        {
        }
    }

    private sealed class ApplicationOwnedDisposalTracker(List<string> disposalOrder) : IDisposable
    {
        public void Dispose() => disposalOrder.Add("application");
    }

    private sealed class ParentApplicationDisposalTracker(List<string> disposalOrder) : IDisposable
    {
        public void Dispose() => disposalOrder.Add("parent-application");
    }

    private sealed class ParentApplicationFactoryWithDerivedDisposalFailure(List<string> disposalOrder) : IDisposable
    {
        private readonly DerivedApplicationFactoryWithOneShotDisposalFailure _derivedApplication = new(disposalOrder);
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _derivedApplication.Dispose();
            disposalOrder.Add("parent-application");
            _disposed = true;
        }
    }

    private sealed class DerivedApplicationFactoryWithOneShotDisposalFailure(List<string> disposalOrder) : IDisposable
    {
        public const string FailureMessage = "Derived application disposal failure.";
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            disposalOrder.Add("derived-application-dispose-failed");
            disposalOrder.Add("derived-application");
            throw new InvalidOperationException(FailureMessage);
        }
    }

    private sealed class StopFailureHostedService(List<string> disposalOrder) : IHostedService
    {
        public const string StopFailureMessage = "Application stop failure.";
        private bool _failNextStop;

        public void FailNextStop() => _failNextStop = true;

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (!_failNextStop)
                return Task.CompletedTask;

            _failNextStop = false;
            disposalOrder.Add("application-stop-failed");
            throw new InvalidOperationException(StopFailureMessage);
        }
    }

    private sealed class StopTrackingHostedService(List<string> disposalOrder) : IHostedService
    {
        private bool _stopped;

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (_stopped)
                return Task.CompletedTask;

            _stopped = true;
            disposalOrder.Add("derived-application-stop");
            return Task.CompletedTask;
        }
    }

    private sealed class RepeatedApplicationFactoryDisposeFailure(List<string> disposalOrder) : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
            disposalOrder.Add($"application-dispose-failed-{DisposeCount}");
            throw new InvalidOperationException($"Application factory dispose failure. Attempt {DisposeCount}.");
        }
    }

    private sealed class OneShotDisposeFailure : IDisposable
    {
        public const string FailureMessage = "One-shot context provider disposal failure.";
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
            if (DisposeCount == 1)
                throw new InvalidOperationException(FailureMessage);
        }
    }

    private sealed class ThrowingTestServerOptions : IOptions<TestServerOptions>, IDisposable
    {
        public const string FailureMessage = "Test server options failure.";
        public int DisposeCount { get; private set; }

        public TestServerOptions Value => throw new InvalidOperationException(FailureMessage);

        public void Dispose() => DisposeCount++;
    }

    private sealed class ExposedDrnWebApplicationFactory(DrnTestContext context)
        : DrnWebApplicationFactory<TemporaryLifecycleProgram>(context, true)
    {
        public IHost CreateHostForTest(IHostBuilder builder) => CreateHost(builder);
    }

    private static ITestOutputHelper? InvokeOutputHelperResolver(ITestOutputHelper? supplied = null)
    {
        var resolver = typeof(ApplicationContext).GetMethod(
            "ResolveOutputHelper",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        return (ITestOutputHelper?)resolver.Invoke(null, [supplied, false]);
    }

    private static WebApplicationFactory<TEntryPoint> InvokeCreateApplicationCore<TEntryPoint>(
        ApplicationContext context,
        ITestOutputHelper? outputHelper)
        where TEntryPoint : class
    {
        var factory = typeof(ApplicationContext)
            .GetMethod("CreateApplicationCore", BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(typeof(TEntryPoint));

        return (WebApplicationFactory<TEntryPoint>)factory.Invoke(context, [outputHelper, null])!;
    }
}
