using System.Collections.Concurrent;
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;

namespace DRN.Test.Utils.Hosting;

public sealed class TemporaryLifecycleProgram : DrnProgramBase<TemporaryLifecycleProgram>, IDrnProgram
{
    private static readonly LifecycleLogCaptureProvider LogCaptureProvider = new();

    public static IAppSettings? CapturedAppSettings { get; private set; }
    public static int CapturedLifecycleLogCount => LogCaptureProvider.Count;

    public static async Task Main(string[] args) => await RunAsync(args);

    public static void Reset()
    {
        CapturedAppSettings = null;
        LogCaptureProvider.Clear();
    }

    protected override void ConfigureApplicationBuilder(WebApplicationBuilder applicationBuilder, IAppSettings appSettings)
    {
        applicationBuilder.Logging.ClearProviders();
        applicationBuilder.Logging.AddProvider(LogCaptureProvider);
        ConfigureWebHostBuilder(appSettings, applicationBuilder.WebHost);
    }

    protected override void ConfigureApplication(WebApplication application, IAppSettings appSettings)
    {
        // This program is only used to capture IAppSettings and verify their lifecycle and disposal behavior.
        // No HTTP middleware pipeline configuration is required, so the default ASP.NET Core middleware pipeline setup is bypassed.
    }

    protected override void ValidateEndpoints(WebApplication application, IAppSettings appSettings)
    {
        // Endpoint discovery and validation are bypassed because this test program does not configure HTTP endpoints.
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

    private sealed class LifecycleLogCaptureProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public int Count => _messages.Count;

        public ILogger CreateLogger(string categoryName) => new LifecycleLogCaptureLogger(categoryName, _messages);

        public void Clear() => _messages.Clear();

        public void Dispose()
        {
        }
    }

    private sealed class LifecycleLogCaptureLogger(string categoryName, ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (categoryName == typeof(TemporaryLifecycleProgram).FullName && IsEnabled(logLevel))
                messages.Enqueue(formatter(state, exception));
        }
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
