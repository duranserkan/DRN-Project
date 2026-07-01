using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler;
using DRN.Framework.Utils.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using DRN.Test.Utils.Hosting;

namespace DRN.Test.Unit.Tests.Framework.Hosting;

public class AppSettingsLifecycleTests
{
    [Fact]
    public async Task DrnProgramBase_RunAsync_Should_Dispose_Startup_AppSettings_When_Temporary_Application_Exits()
    {
        TemporaryLifecycleProgram.Reset();

        await TemporaryLifecycleProgram.Main(CreateTemporaryApplicationArgs());

        var appSettings = TemporaryLifecycleProgram.CapturedAppSettings;
        appSettings.Should().NotBeNull();
        var defaultKey = appSettings!.NexusAppSettings.GetDefaultKey();
        var action = () => { _ = defaultKey.MacKey.Bytes; };
        action.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task DrnProgramBase_StartupExceptionReport_Should_Dispose_Temporary_ServiceProvider()
    {
        StartupExceptionReportProgram.Reset();

        Func<Task> run = () => StartupExceptionReportProgram.Main(CreateTemporaryApplicationArgs());

        await run.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage(StartupExceptionReportProgram.FailureMessage);
        StartupExceptionReportProgram.ReportServiceDisposeCount.Should().Be(1);
    }

    private static string[] CreateTemporaryApplicationArgs() =>
    [
        "--Environment=Development",
        "--DrnDevelopmentSettings:TemporaryApplication=true",
        "--NLog:targets:console:type=Console",
        "--NLog:rules:0:logger=*",
        "--NLog:rules:0:minLevel=Trace",
        "--NLog:rules:0:writeTo=console"
    ];
}
