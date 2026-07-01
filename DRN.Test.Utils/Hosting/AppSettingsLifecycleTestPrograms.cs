using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;

namespace DRN.Test.Utils.Hosting;

public sealed class TemporaryLifecycleProgram : DrnProgramBase<TemporaryLifecycleProgram>, IDrnProgram
{
    public static IAppSettings? CapturedAppSettings { get; private set; }

    public static async Task Main(string[] args) => await RunAsync(args);

    public static void Reset() => CapturedAppSettings = null;

    protected override void ConfigureApplicationBuilder(WebApplicationBuilder applicationBuilder, IAppSettings appSettings)
    {
        ConfigureWebHostBuilder(appSettings, applicationBuilder.WebHost);
    }

    protected override void ConfigureApplication(WebApplication application, IAppSettings appSettings)
    {
    }

    protected override Task ValidateServicesAsync(WebApplication application, IScopedLog scopeLog) => Task.CompletedTask;

    protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog)
    {
        CapturedAppSettings = appSettings;
        if (appSettings.NexusAppSettings.GetDefaultKey().MacKey.Bytes.Length == 0)
        {
            throw new InvalidOperationException("MacKey Bytes should not be empty.");
        }

        return Task.CompletedTask;
    }
}

public sealed class StartupExceptionReportProgram : DrnProgramBase<StartupExceptionReportProgram>, IDrnProgram
{
    public const string FailureMessage = "Startup failure for service provider disposal test.";
    private static int _addServicesCallCount;

    public static int ReportServiceDisposeCount => StartupExceptionReportDisposable.DisposeCount;

    public static async Task Main(string[] args) => await RunAsync(args);

    public static void Reset()
    {
        _addServicesCallCount = 0;
        StartupExceptionReportDisposable.Reset();
    }

    protected override void ConfigureApplicationBuilder(WebApplicationBuilder applicationBuilder, IAppSettings appSettings)
    {
        ConfigureWebHostBuilder(appSettings, applicationBuilder.WebHost);
    }

    protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog)
    {
        if (Interlocked.Increment(ref _addServicesCallCount) == 1)
            throw new InvalidOperationException(FailureMessage);

        builder.Services.AddSingleton<StartupExceptionReportDisposable>();
        builder.Services.AddSingleton<IDrnExceptionHandler, StartupExceptionReportExceptionHandler>();

        return Task.CompletedTask;
    }
}

public sealed class StartupExceptionReportExceptionHandler : IDrnExceptionHandler
{
    public Task HandleExceptionAsync(HttpContext context, Exception ex) => throw new NotSupportedException();

    public Task<ExceptionPageModel> GetExceptionPageModel(IServiceProvider serviceProvider, Exception exception) => throw new NotSupportedException();

    public Task<ExceptionContentResult?> GetExceptionContentAsync(IServiceProvider serviceProvider, Exception exception) =>
        throw new NotSupportedException();

    public Task<ExceptionContentResult?> GetStartupExceptionContentAsync(IServiceProvider serviceProvider, Exception exception, IScopedLog startupLog)
    {
        _ = serviceProvider.GetRequiredService<StartupExceptionReportDisposable>();

        return Task.FromResult<ExceptionContentResult?>(null);
    }
}

public sealed class StartupExceptionReportDisposable : IDisposable
{
    private static int _disposeCount;

    public static int DisposeCount => _disposeCount;

    public static void Reset() => _disposeCount = 0;

    public void Dispose() => Interlocked.Increment(ref _disposeCount);
}
