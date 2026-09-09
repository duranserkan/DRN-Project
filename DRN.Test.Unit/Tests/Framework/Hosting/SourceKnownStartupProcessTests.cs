using System.Diagnostics;
using DRN.Test.Utils.Testing;

namespace DRN.Test.Unit.Tests.Framework.Hosting;

public class SourceKnownStartupProcessTests
{
    [Theory]
    [DataInlineUnit("startup-only")]
    [DataInlineUnit("startup-reject")]
    [DataInlineUnit("static")]
    [DataInlineUnit("static-reject")]
    [DataInlineUnit("historical-freeze")]
    public async Task Startup_And_Generation_Respect_Configuration_In_A_Fresh_Process(string scenario)
    {
        var testAssembly = typeof(SourceKnownStartupProcessTests).Assembly.Location;
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = AppContext.BaseDirectory
        };
        // Reuse the test runner's runtime/dependency graph; the support DLL has its own entry point.
        start.ArgumentList.Add("exec");
        start.ArgumentList.Add("--runtimeconfig");
        start.ArgumentList.Add(Path.ChangeExtension(testAssembly, ".runtimeconfig.json"));
        start.ArgumentList.Add("--depsfile");
        start.ArgumentList.Add(Path.ChangeExtension(testAssembly, ".deps.json"));
        start.ArgumentList.Add(typeof(TestUtilsPlaceholderProgram).Assembly.Location);
        start.ArgumentList.Add("--verify-source-known-startup");
        start.ArgumentList.Add(scenario);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the test-support process.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw new TimeoutException($"Startup verification timed out for {scenario}.");
        }

        var output = await outputTask;
        var error = await errorTask;
        process.ExitCode.Should().Be(0, $"scenario {scenario} must succeed; stdout: {output}; stderr: {error}");
        output.Should().Contain("Source-Known verification passed.");
    }
}
