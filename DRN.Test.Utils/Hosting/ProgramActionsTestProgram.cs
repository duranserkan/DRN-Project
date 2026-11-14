using System.Collections.Concurrent;
using System.Reflection;
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Utils.DependencyInjection;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;

namespace DRN.Test.Utils.Hosting;

public sealed class ProgramActionsTestProgram : DrnProgramBase<ProgramActionsTestProgram>, IDrnProgram
{
    public static Task Main(string[] args) => RunAsync(args);

    protected override void ConfigureApplicationBuilder(WebApplicationBuilder builder, IAppSettings appSettings)
    {
        builder.Logging.ClearProviders();
        ConfigureWebHostBuilder(appSettings, builder.WebHost);
        builder.Services.AddSingleton(appSettings);
    }

    protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog)
        => Task.CompletedTask;

    protected override void ConfigureApplication(WebApplication application, IAppSettings appSettings)
    {
        var probe = application.Services.GetRequiredService<ProgramActionsProbe>();
        probe.Application = application;
        probe.Events.Enqueue("pipeline");
    }

    protected override void ValidateEndpoints(WebApplication application, IAppSettings appSettings)
    {
        application.Services.GetRequiredService<ProgramActionsProbe>().Events.Enqueue("endpoints");
        // No endpoints are configured. Temporary hosts must return without resolving endpoint services.
        if (appSettings.DevelopmentSettings.TemporaryApplication)
            base.ValidateEndpoints(application, appSettings);
    }

    protected override async Task ValidateServicesAsync(WebApplication application, IScopedLog scopeLog)
    {
        var probe = application.Services.GetRequiredService<ProgramActionsProbe>();
        probe.Events.Enqueue("services:start");
        await base.ValidateServicesAsync(application, scopeLog);
        probe.Events.Enqueue("services:end");
    }
}

public sealed class ProgramActionsProbe
{
    public ConcurrentQueue<string> Events { get; } = new();
    public ProgramActionGate[] Gates { get; } = [new(), new(), new()];
    public ProgramActionGate ValidationGate { get; } = new();
    public List<(object Program, IAppSettings Settings, IScopedLog Log)> Arguments { get; } = [];
    public List<DrnProgramActions> ActionInstances { get; } = [];
    public WebApplicationBuilder? Builder { get; set; }
    public WebApplication? Application { get; set; }
    public int? FailingHook { get; init; }
    public bool FailValidation { get; init; }
    public bool PauseValidation { get; init; }
    public bool FailAsyncValidation { get; init; }
    public Exception Failure { get; } = new InvalidOperationException("Action lifecycle probe failure");
    public int ValidationResolutions { get; private set; }

    public void ConfigureBuilder(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(this);
        builder.Services.AddTransient<ProgramActionsValidationProbe>(_ =>
        {
            ValidationResolutions++;
            if (FailValidation) throw Failure;
            return new ProgramActionsValidationProbe();
        });
        builder.Services.AddSingleton(new DrnServiceContainer(typeof(ProgramActionsProbe).Assembly,
            [new TransientAttribute<ProgramActionsValidationProbe>()],
            PauseValidation
                ? [new AttributeSpecifiedServiceModule([ServiceDescriptor.Singleton(this)], new ProgramActionsValidationModule())]
                : []));
        Events.Enqueue("builder:configured");
    }

    public async Task InvokeAsync(int hook, DrnProgramActions actions, object program, IAppSettings settings, IScopedLog log)
    {
        ActionInstances.Add(actions);
        Arguments.Add((program, settings, log));
        Events.Enqueue($"hook:{hook}:start");
        Gates[hook].Entered.TrySetResult();
        await Gates[hook].Release.Task;
        if (FailingHook == hook) throw Failure;
        Events.Enqueue($"hook:{hook}:end");
    }

    public void ReleaseAll()
    {
        foreach (var gate in Gates)
            gate.Release.TrySetResult();
        ValidationGate.Release.TrySetResult();
    }

    public async Task ValidateAsync()
    {
        Events.Enqueue("validation:start");
        ValidationGate.Entered.TrySetResult();
        await ValidationGate.Release.Task;
        if (FailAsyncValidation) throw Failure;
        Events.Enqueue("validation:end");
    }
}

public sealed class ProgramActionsValidationModule : ServiceRegistrationAttribute
{
    // The probe supplies the module descriptors explicitly to isolate this validation fixture.
    public override void ServiceRegistration(IServiceCollection sc, Assembly? assembly) { }

    public override Task PostStartupValidationAsync(object service, IServiceProvider serviceProvider, IScopedLog? scopedLog = null)
        => ((ProgramActionsProbe)service).ValidateAsync();
}

public sealed class ProgramActionGate
{
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

public sealed class ProgramActionsValidationProbe;

// Discovery is assembly-wide. Other support programs must retain their existing lifecycle behavior.
public sealed class LifecycleTestActions : DrnProgramActions
{
    public override Task ApplicationBuilderCreatedAsync<TProgram>(TProgram program, WebApplicationBuilder builder,
        IAppSettings appSettings, IScopedLog scopedLog)
    {
        if (program is not ProgramActionsTestProgram) return Task.CompletedTask;
        var probe = (ProgramActionsProbe)builder.Services.Single(s => s.ServiceType == typeof(ProgramActionsProbe)).ImplementationInstance!;
        probe.Builder = builder;
        return probe.InvokeAsync(0, this, program, appSettings, scopedLog);
    }

    public override Task ApplicationBuiltAsync<TProgram>(TProgram program, WebApplication application,
        IAppSettings appSettings, IScopedLog scopedLog)
        => program is ProgramActionsTestProgram
            ? application.Services.GetRequiredService<ProgramActionsProbe>().InvokeAsync(1, this, program, appSettings, scopedLog)
            : Task.CompletedTask;

    public override Task ApplicationValidatedAsync<TProgram>(TProgram program, WebApplication application,
        IAppSettings appSettings, IScopedLog scopedLog)
        => program is ProgramActionsTestProgram
            ? application.Services.GetRequiredService<ProgramActionsProbe>().InvokeAsync(2, this, program, appSettings, scopedLog)
            : Task.CompletedTask;
}
