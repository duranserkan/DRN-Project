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
    public async Task DrnProgramBase_RunAsync_Should_Log_And_Dispose_Startup_AppSettings_When_Temporary_Application_Exits()
    {
        TemporaryLifecycleProgram.Reset();

        await TemporaryLifecycleProgram.Main(CreateTemporaryApplicationArgs());

        TemporaryLifecycleProgram.CapturedLifecycleLogCount.Should().BeGreaterThan(0);
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
        StartupExceptionReportProgram.AsyncDisposalCompleted.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit("builder")]
    [DataInlineUnit("generation")]
    [DataInlineUnit("disposal")]
    [DataInlineUnit("both")]
    public async Task StartupReport_Failures_Should_Not_Replace_The_Original_Startup_Exception(string failureMode)
    {
        StartupExceptionReportProgram.Reset(failureMode);
        var original = StartupExceptionReportProgram.StartupFailure;
        try
        {
            Func<Task> run = () => StartupExceptionReportProgram.Main(CreateTemporaryApplicationArgs());

            var thrown = await run.Should().ThrowExactlyAsync<InvalidOperationException>();

            thrown.Which.Should().BeSameAs(original);
            StartupExceptionReportProgram.ReportCalls.Should().Be(failureMode == "builder" ? 0 : 1);
            StartupExceptionReportProgram.AsyncDisposalCompleted.Should().Be(failureMode != "builder");
        }
        finally
        {
            StartupExceptionReportProgram.Reset();
        }
    }

    // DrnProgramBase requires an "NLog" configuration section during bootstrap.
    // Configure a Null target with minLevel=Off so expected test startup exceptions do not pollute test console output.
    private static string[] CreateTemporaryApplicationArgs() =>
    [
        "--Environment=Development",
        "--DrnDevelopmentSettings:TemporaryApplication=true",
        "--NLog:targets:null:type=Null",
        "--NLog:rules:0:logger=*",
        "--NLog:rules:0:minLevel=Off",
        "--NLog:rules:0:writeTo=null"
    ];
}
