using System.Net;
using System.Net.Http.Json;
using DRN.Framework.Testing.Contexts.Application;
using DRN.Framework.Utils.Models.Sample;
using DRN.Nexus.Hosted;
using DRN.Test.Utils.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Sample.Hosted;
using Sample.Hosted.Filters;
using Sample.Hosted.Helpers;
using Sample.Infra.QA;
using NexusGet = DRN.Nexus.Hosted.Helpers.Get;
// ReSharper disable AccessToDisposedClosure
// ReSharper disable ShortLivedHttpClient

namespace DRN.Test.Integration.Tests.Framework.Testing;

public class ApplicationContextTests
{
    [Fact]
    public void ApplicationContext_Should_Resolve_Active_Xunit_Output_Helper()
    {
        var ambientOutputHelper = TestContext.Current.TestOutputHelper;

        ambientOutputHelper.Should().NotBeNull();
        ApplicationContext.ResolveOutputHelper(debuggerOnly: false).Should().BeSameAs(ambientOutputHelper);
    }

    [Fact]
    public void ApplicationContext_Should_Prefer_Explicit_Output_Helper()
    {
        var suppliedOutputHelper = Substitute.For<ITestOutputHelper>();

        ApplicationContext.ResolveOutputHelper(suppliedOutputHelper, debuggerOnly: false).Should().BeSameAs(suppliedOutputHelper);
    }

    [Fact]
    public async Task ApplicationContext_Should_Allow_Missing_Output_Helper_Outside_Active_Test_Context()
    {
        Task<ITestOutputHelper?> resolutionTask;
        using (ExecutionContext.SuppressFlow())
            resolutionTask = Task.Run(() => ApplicationContext.ResolveOutputHelper(debuggerOnly: false));

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
            context.ApplicationContext.CreateApplicationCore<TemporaryLifecycleProgram>(outputHelper);
        _ = firstApplication.Server;
        firstApplication.Services.GetRequiredService<ILogger<ApplicationContextTests>>()
            .LogCritical(firstApplicationMessage);

        outputHelper.Received().WriteLine(
            Arg.Is<string>(message => message != null && message.Contains(firstApplicationMessage, StringComparison.Ordinal)));

        var secondApplication =
            context.ApplicationContext.CreateApplicationCore<TemporaryLifecycleProgram>(null);
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
        hostedService.FailAllStops();

        var dispose = context.Dispose;

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
        hostedService.FailAllStops();

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
        context.ApplicationContext.UseApplicationFactory<TemporaryLifecycleProgram>(factory);
        context.ApplicationContext.GetCreatedApplication<TemporaryLifecycleProgram>().Should().BeNull();

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
        context.ApplicationContext.UseApplicationFactory<TemporaryLifecycleProgram>(factory);

        var dispose = context.Dispose;

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
        var throwingHostedService = new ThrowingHostedService();

        var createApplication = () => context.ApplicationContext.CreateApplication<TemporaryLifecycleProgram>(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<IHostedService>(_ => throwingHostedService)));

        createApplication.Should().ThrowExactly<InvalidOperationException>()
            .WithMessage(ThrowingHostedService.FailureMessage);
        throwingHostedService.DisposeCount.Should().Be(1);
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
    public void DrnWebApplicationFactory_HostConfiguration_Should_Reach_Startup(DrnTestContext context)
    {
        TemporaryLifecycleProgram.Reset();

        using var factory = new ExposedDrnWebApplicationFactory(context);
        var builder = factory.CreateHostBuilderForTest();
        builder.Should().NotBeNull();

        string? hostContextValue = null;
        builder!.ConfigureHostConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CustomHostConfigKey"] = "CustomHostConfigValue"
        }));
        builder.ConfigureAppConfiguration((hostContext, _) =>
        {
            hostContextValue = hostContext.Configuration["CustomHostConfigKey"];
        });

        using var host = builder.Build();
        var configuration = host.Services.GetRequiredService<IConfiguration>();

        hostContextValue.Should().Be("CustomHostConfigValue");
        configuration["CustomHostConfigKey"].Should().Be("CustomHostConfigValue");
        TemporaryLifecycleProgram.CapturedAppSettings?.GetValue<string>("CustomHostConfigKey").Should().Be("CustomHostConfigValue");
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
        await using var factory = new ExposedDrnWebApplicationFactory(context);
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
    public void ApplicationContext_Should_Dispose_Secondary_Program_Startup_AppSettings(DrnTestContext context)
    {
        TemporaryLifecycleProgram.Reset();

        var application = context.ApplicationContext.CreateApplication<TemporaryLifecycleProgram>();
        _ = application.Server;

        var appSettings = TemporaryLifecycleProgram.CapturedAppSettings;
        appSettings.Should().NotBeNull();
        var defaultKey = appSettings!.NexusAppSettings.GetDefaultKey();

        context.ApplicationContext.Dispose();

        Action readKeyMaterial = () => _ = defaultKey.MacKey.Bytes;
        readKeyMaterial.Should().Throw<ObjectDisposedException>();
    }

    [Theory]
    [DataInline]
    public async Task ApplicationContext_Should_Apply_Resolved_WebRoot_To_Secondary_Program(DrnTestContext context)
    {
        SecondaryWebRootProgram.Reset();

        using var client = await context.ApplicationContext.CreateClientAsync<SecondaryWebRootProgram>();

        SecondaryWebRootProgram.ResolvedWebRootPath.Should().NotBeNullOrWhiteSpace();
        SecondaryWebRootProgram.ResolvedWebRootPath.Should().EndWith(Path.Combine("DRN.Test.Utils", "wwwroot"));
        Directory.Exists(SecondaryWebRootProgram.ResolvedWebRootPath).Should().BeTrue();

        SecondaryWebRootProgram.ResolvedViteManifest.Should().NotBeNull();
        SecondaryWebRootProgram.ResolvedViteManifest.Should().NotBeOfType<EmptyViteManifest>();
        var manifestItem = SecondaryWebRootProgram.ResolvedViteManifest!.GetManifestItem("test-asset.txt");
        manifestItem.Should().NotBeNull();
        manifestItem!.File.Should().Be("test-asset.txt");

        using var response = await client.GetAsync("/test-asset.txt");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Trim().Should().Be("test-asset");
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

    [Theory]
    [DataInline]
    public async Task ApplicationContext_Should_Host_Multiple_Concurrent_Applications(DrnTestContext context)
    {
        var nexusClient = await context.ApplicationContext.CreateClientAsync<NexusProgram>();
        var sampleClient = await context.ApplicationContext.CreateClientAsync<SampleProgram>();

        context.ApplicationContext.GetCreatedApplication<NexusProgram>().Should().NotBeNull();
        context.ApplicationContext.GetCreatedApplication<SampleProgram>().Should().NotBeNull();
        context.ApplicationContext.GetCreatedApplications().Should().HaveCount(2);

        var nexusForecasts = await nexusClient.GetFromJsonAsync<WeatherForecast[]>(NexusGet.Endpoint.Sample.WeatherForecast.Get.RoutePattern);
        nexusForecasts.Should().NotBeNull();
        nexusForecasts.Length.Should().BePositive();

        var sampleForecasts = await sampleClient.GetFromJsonAsync<WeatherForecast[]>(Get.Endpoint.Sample.WeatherForecast.Get.RoutePattern);
        sampleForecasts.Should().NotBeNull();
        sampleForecasts.Length.Should().BePositive();
    }

    [Theory]
    [DataInline]
    public async Task ApplicationContext_Should_Route_Requests_Across_Applications_Via_RouterHandler(DrnTestContext context)
    {
        _ = await context.ApplicationContext.CreateClientAsync<NexusProgram>();
        await context.ApplicationContext.CreateApplicationAndBindDependenciesAsync<SampleProgram>();

        using var client = new HttpClient(context.ApplicationContext.RouterHandler, disposeHandler: false);

        // Call Nexus via configured address (e.g. localhost:5988 or nexus)
        var appSettings = context.GetRequiredService<IAppSettings>();
        var nexusEndpoint = NexusGet.Endpoint.Sample.WeatherForecast.Get.RoutePattern;
        var nexusUri = $"http://{appSettings.NexusAppSettings.NexusAddress}/{nexusEndpoint?.TrimStart('/')}";
        var nexusForecasts = await client.GetFromJsonAsync<WeatherForecast[]>(nexusUri);
        nexusForecasts.Should().NotBeNull();
        nexusForecasts.Length.Should().BePositive();

        // Call Sample via sample hostname
        var sampleEndpoint = Get.Endpoint.Sample.WeatherForecast.Get.RoutePattern;
        var sampleUri = $"http://sample/{sampleEndpoint?.TrimStart('/')}";
        var sampleForecasts = await client.GetFromJsonAsync<WeatherForecast[]>(sampleUri);
        sampleForecasts.Should().NotBeNull();
        sampleForecasts.Length.Should().BePositive();
    }

    [Theory]
    [DataInline]
    public async Task ApplicationContext_Should_Route_To_Configured_Custom_Nexus_Address(DrnTestContext context)
    {
        var customNexusSettings = new NexusAppSettings
        {
            AppId = 7,
            AppInstanceId = 14,
            NexusAddress = "custom-nexus-host"
        };

        context.AddToConfiguration(new { NexusAppSettings = customNexusSettings });
        var nexusClient = await context.ApplicationContext.CreateClientAsync<NexusProgram>();
        nexusClient.Should().NotBeNull();

        var appSettings = context.GetRequiredService<IAppSettings>();
        appSettings.NexusAppSettings.AppId.Should().Be(7);
        appSettings.NexusAppSettings.AppInstanceId.Should().Be(14);
        appSettings.NexusAppSettings.NexusAddress.Should().Be("custom-nexus-host");

        // RouterHandler can route to Nexus using configured address
        using var client = new HttpClient(context.ApplicationContext.RouterHandler, disposeHandler: false);
        var endpoint = NexusGet.Endpoint.Sample.WeatherForecast.Get.RoutePattern;
        var forecasts = await client.GetFromJsonAsync<WeatherForecast[]>($"http://custom-nexus-host/{endpoint?.TrimStart('/')}");
        forecasts.Should().NotBeNull();
        forecasts.Length.Should().BePositive();
    }

    [Theory]
    [DataInline]
    public async Task ApplicationContext_Should_Discover_Addresses_Only_From_Matching_Key_Segments(DrnTestContext context)
    {
        // 1. Exact short-name segment match in hierarchical key
        context.AddToConfiguration("Services:Nexus:Url", "http://nexus-segment-url");
        // 2. Exact type-name segment match
        context.AddToConfiguration("NexusProgram:Address", "http://nexus-program-address");
        // 3. Suffix on short-name (NexusAddress)
        context.AddToConfiguration("Custom:NexusAddress", "http://nexus-suffix-address");
        // 4. Substring in segment (should NOT match)
        context.AddToConfiguration("ExternalNexusAddress", "http://external-nexus-address");
        context.AddToConfiguration("ExternalPaymentUrl", "http://external-payment-url");
        context.AddToConfiguration("ExternalNexusSettings:ServiceUrl", "http://external-nexus-service-url");
        context.AddToConfiguration("Nexus:ExternalPaymentUrl", "http://nested-external-payment-url");
        // 5. Non-address key matching short name (should NOT match)
        context.AddToConfiguration("Nexus:Name", "ignored-value");

        _ = await context.ApplicationContext.CreateClientAsync<NexusProgram>();

        using var client = new HttpClient(context.ApplicationContext.RouterHandler, disposeHandler: false);
        var endpoint = NexusGet.Endpoint.Sample.WeatherForecast.Get.RoutePattern;

        // Valid segment matches route successfully
        var r1 = await client.GetFromJsonAsync<WeatherForecast[]>($"http://nexus-segment-url/{endpoint?.TrimStart('/')}");
        r1.Should().NotBeNull();
        r1.Length.Should().BePositive();

        var r2 = await client.GetFromJsonAsync<WeatherForecast[]>($"http://nexus-program-address/{endpoint?.TrimStart('/')}");
        r2.Should().NotBeNull();
        r2.Length.Should().BePositive();

        var r3 = await client.GetFromJsonAsync<WeatherForecast[]>($"http://nexus-suffix-address/{endpoint?.TrimStart('/')}");
        r3.Should().NotBeNull();
        r3.Length.Should().BePositive();

        // Substring / non-segment matches are not registered and throw unmapped host
        Func<Task> callExternal = () => client.GetAsync("http://external-nexus-address/api/test");
        await callExternal.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No application registered in ApplicationContext matches host 'external-nexus-address'*");

        Func<Task> callPayment = () => client.GetAsync("http://external-payment-url/api/test");
        await callPayment.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No application registered in ApplicationContext matches host 'external-payment-url'*");

        Func<Task> callSettings = () => client.GetAsync("http://external-nexus-service-url/api/test");
        await callSettings.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No application registered in ApplicationContext matches host 'external-nexus-service-url'*");

        Func<Task> callNestedPayment = () => client.GetAsync("http://nested-external-payment-url/api/test");
        await callNestedPayment.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No application registered in ApplicationContext matches host 'nested-external-payment-url'*");
    }

    [Theory]
    [DataInline]
    public async Task ApplicationContext_Should_Support_MultiHop_Chained_Applications(DrnTestContext context)
    {
        // Setup 3 chained apps: Node A -> Node B -> Node C with service names assigned at creation
        _ = await context.ApplicationContext.CreateClientForServiceAsync<ChainedNodeCProgram>(ChainedNodeCProgram.NodeName);
        _ = await context.ApplicationContext.CreateClientForServiceAsync<ChainedNodeBProgram>(ChainedNodeBProgram.NodeName);
        var nodeAClient = await context.ApplicationContext.CreateClientForServiceAsync<ChainedNodeAProgram>(ChainedNodeAProgram.NodeName);

        // Call Node A -> Node A calls Node B -> Node B calls Node C -> Node C returns "C" -> Node B returns "B -> C" -> Node A returns "A -> B -> C"
        var response = await nodeAClient.GetStringAsync("/api/chain/start");
        response.Should().Contain("A -> B -> C");
    }

    [Theory]
    [DataInline]
    public async Task ApplicationContext_Should_Support_Explicit_MapAddress(DrnTestContext context)
    {
        _ = await context.ApplicationContext.CreateClientAsync<NexusProgram>();

        context.ApplicationContext.MapAddress<NexusProgram>("weather-service");

        using var client = new HttpClient(context.ApplicationContext.RouterHandler, disposeHandler: false);
        var endpoint = NexusGet.Endpoint.Sample.WeatherForecast.Get.RoutePattern;
        var forecasts = await client.GetFromJsonAsync<WeatherForecast[]>($"http://weather-service/{endpoint?.TrimStart('/')}");
        forecasts.Should().NotBeNull();
        forecasts.Length.Should().BePositive();
    }

    [Theory]
    [DataInline]
    public async Task ApplicationContext_Should_Support_Bidirectional_Service_Communication(DrnTestContext context)
    {
        var node1Client = await context.ApplicationContext.CreateClientForServiceAsync<BidirectionalNode1Program>(BidirectionalNode1Program.NodeName);
        var node2Client = await context.ApplicationContext.CreateClientForServiceAsync<BidirectionalNode2Program>(BidirectionalNode2Program.NodeName);

        // Node 1 calls Node 2
        var node1Response = await node1Client.GetStringAsync("/api/node1/ping");
        node1Response.Should().Contain("1-ping(2-pong)");

        // Node 2 calls Node 1
        var node2Response = await node2Client.GetStringAsync("/api/node2/ping");
        node2Response.Should().Contain("2-ping(1-pong)");
    }

    [Theory]
    [DataInline]
    public async Task ApplicationContext_RouterHandler_Should_Throw_Detailed_Exception_On_Unmapped_Host(DrnTestContext context)
    {
        _ = await context.ApplicationContext.CreateClientAsync<NexusProgram>();
        _ = await context.ApplicationContext.CreateClientAsync<SampleProgram>();

        using var client = new HttpClient(context.ApplicationContext.RouterHandler, disposeHandler: false);
        Func<Task> call = () => client.GetAsync("http://unmapped-unknown-service/api/test");

        var ex = await call.Should().ThrowAsync<InvalidOperationException>();
        ex.WithMessage("*No application registered in ApplicationContext matches host 'unmapped-unknown-service'*")
            .WithMessage("*Registered addresses:*")
            .WithMessage("*Registered applications:*");
    }

    [Theory]
    [DataInline]
    public async Task ApplicationContext_RouterHandler_Should_Route_IPv6_Addresses_And_Ports(DrnTestContext context)
    {
        _ = await context.ApplicationContext.CreateClientForServiceAsync<NexusProgram>("[::1]:5999");

        using var client = new HttpClient(context.ApplicationContext.RouterHandler, disposeHandler: false);
        var endpoint = NexusGet.Endpoint.Sample.WeatherForecast.Get.RoutePattern;

        // Route via bracketed IPv6 + port
        var forecasts1 = await client.GetFromJsonAsync<WeatherForecast[]>($"http://[::1]:5999/{endpoint?.TrimStart('/')}");
        forecasts1.Should().NotBeNull();
        forecasts1.Length.Should().BePositive();

        // Route via port alone
        var forecasts2 = await client.GetFromJsonAsync<WeatherForecast[]>($"http://localhost:5999/{endpoint?.TrimStart('/')}");
        forecasts2.Should().NotBeNull();
        forecasts2.Length.Should().BePositive();
    }

    [Theory]
    [DataInline]
    public async Task ApplicationContext_RouterHandler_Should_Not_Route_Unrelated_Host_When_Default_Port_Is_Registered(DrnTestContext context)
    {
        _ = await context.ApplicationContext.CreateClientForServiceAsync<NexusProgram>("http://nexus:80");

        using var client = new HttpClient(context.ApplicationContext.RouterHandler, disposeHandler: false);
        Func<Task> callUnrelatedHttp = () => client.GetAsync("http://unrelated-domain/api/test");
        Func<Task> callUnrelatedHttps = () => client.GetAsync("https://unrelated-domain/api/test");

        await callUnrelatedHttp.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No application registered in ApplicationContext matches host 'unrelated-domain'*");
        await callUnrelatedHttps.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No application registered in ApplicationContext matches host 'unrelated-domain'*");
    }

    [Theory]
    [DataInline]
    public async Task ApplicationContext_Replacing_One_Application_Should_Not_Disrupt_Remaining_Application_Routing(DrnTestContext context)
    {
        // 1. Create two independent nodes
        var node1Client = await context.ApplicationContext.CreateClientForServiceAsync<BidirectionalNode1Program>(BidirectionalNode1Program.NodeName);
        _ = await context.ApplicationContext.CreateClientForServiceAsync<BidirectionalNode2Program>(BidirectionalNode2Program.NodeName);

        // Verify initial routing from node-1 to node-2
        var initialResponse = await node1Client.GetStringAsync("/api/node1/ping");
        initialResponse.Should().Contain("1-ping(2-pong)");

        // 2. Replace node-1 (recreation disposes the old node-1 application)
        var newNode1Client = await context.ApplicationContext.CreateClientForServiceAsync<BidirectionalNode1Program>(BidirectionalNode1Program.NodeName);

        // 3. Verify node-2 is still routable from the new node-1 instance and router handler is intact
        var postReplacementResponse = await newNode1Client.GetStringAsync("/api/node1/ping");
        postReplacementResponse.Should().Contain("1-ping(2-pong)");
    }

    [Theory]
    [DataInline]
    public async Task ApplicationContext_MapAddress_Recreated_Application_Should_Route_To_New_Instance(DrnTestContext context)
    {
        _ = await context.ApplicationContext.CreateClientAsync<NexusProgram>();
        context.ApplicationContext.MapAddress<NexusProgram>("weather-alias");

        using var client = new HttpClient(context.ApplicationContext.RouterHandler, disposeHandler: false);
        var endpoint = NexusGet.Endpoint.Sample.WeatherForecast.Get.RoutePattern;
        var forecasts1 = await client.GetFromJsonAsync<WeatherForecast[]>($"http://weather-alias/{endpoint?.TrimStart('/')}");
        forecasts1.Should().NotBeNull();
        forecasts1.Length.Should().BePositive();

        // Recreate NexusProgram (disposes old factory and registers new one)
        _ = await context.ApplicationContext.CreateClientAsync<NexusProgram>();

        // Mapped address should resolve to the newly registered instance without errors
        var forecasts2 = await client.GetFromJsonAsync<WeatherForecast[]>($"http://weather-alias/{endpoint?.TrimStart('/')}");
        forecasts2.Should().NotBeNull();
        forecasts2.Length.Should().BePositive();
    }

    [Theory]
    [DataInline]
    public async Task ApplicationContext_Overload_Compatibility_Should_Resolve_Without_Ambiguity(DrnTestContext context)
    {
        // Execute one runtime creation to verify baseline pipeline functionality
        var client = await context.ApplicationContext.CreateClientAsync<NexusProgram>();
        client.Should().NotBeNull();

        // Compile-time overload resolution checks for remaining call shapes without starting extra test servers
        var compileTimeOverloadCheck = () =>
        {
            _ = context.ApplicationContext.CreateClientAsync<NexusProgram>(outputHelper: null);
            _ = context.ApplicationContext.CreateClientAsync<NexusProgram>(outputHelper: null, clientOptions: null);
            _ = context.ApplicationContext.CreateClientForServiceAsync<NexusProgram>("nexus-overload-test");
            _ = context.ApplicationContext.CreateApplication<TemporaryLifecycleProgram>();
            _ = context.ApplicationContext.CreateApplicationAndBindDependenciesAsync<TemporaryLifecycleProgram>();
            _ = new DrnWebApplicationFactory<TemporaryLifecycleProgram>(context, temporary: true);
        };
        compileTimeOverloadCheck.Should().NotBeNull();
    }

    [Theory]
    [DataInline]
    public async Task ApplicationContext_MapAddress_With_Url_Path_Should_Route_To_Authority(DrnTestContext context)
    {
        _ = await context.ApplicationContext.CreateClientAsync<NexusProgram>();
        context.ApplicationContext.MapAddress<NexusProgram>("https://custom-nexus-host:5988/api/v1");

        using var client = new HttpClient(context.ApplicationContext.RouterHandler, disposeHandler: false);
        var endpoint = NexusGet.Endpoint.Sample.WeatherForecast.Get.RoutePattern;
        var forecasts = await client.GetFromJsonAsync<WeatherForecast[]>($"http://custom-nexus-host:5988/{endpoint?.TrimStart('/')}");
        forecasts.Should().NotBeNull();
        forecasts.Length.Should().BePositive();
    }

    private sealed class ContextOwnedDisposalTracker(List<string> disposalOrder) : IDisposable
    {
        public void Dispose() => disposalOrder.Add("context");
    }

    // ReSharper disable once NotAccessedPositionalProperty.Local
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
        private readonly Lock _sync = new();
        private bool _failAllStops;
        private bool _failureRecorded;

        public void FailAllStops()
        {
            lock (_sync)
                _failAllStops = true;
        }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                if (!_failAllStops)
                    return Task.CompletedTask;

                // WebApplicationFactory and the entry point's WaitForShutdownAsync can stop the same host
                // concurrently. Keep the failure armed for both callers, but record the event only once.
                if (!_failureRecorded)
                {
                    disposalOrder.Add("application-stop-failed");
                    _failureRecorded = true;
                }
            }

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

    private sealed class ThrowingHostedService : IHostedService, IDisposable
    {
        public const string FailureMessage = "Hosted service failure.";
        public int DisposeCount { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException(FailureMessage);

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Dispose() => DisposeCount++;
    }

    private sealed class ExposedDrnWebApplicationFactory(DrnTestContext context)
        : DrnWebApplicationFactory<TemporaryLifecycleProgram>(context, true)
    {
        public IHost CreateHostForTest(IHostBuilder builder) => CreateHost(builder);
        public IHostBuilder? CreateHostBuilderForTest() => CreateHostBuilder();
    }
}
