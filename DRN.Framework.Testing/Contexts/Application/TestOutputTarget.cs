using System.Diagnostics;
using NLog;
using NLog.Targets;

namespace DRN.Framework.Testing.Contexts.Application;

/// <summary>
/// Custom NLog target that formats and routes application log events directly to xUnit's <see cref="ITestOutputHelper"/>.
/// </summary>
public sealed class TestOutputTarget : TargetWithLayout
{
    private readonly ITestOutputHelper _testOutputHelper;

    public TestOutputTarget(ITestOutputHelper testOutputHelper, string? testName = null)
    {
        _testOutputHelper = testOutputHelper ?? throw new ArgumentNullException(nameof(testOutputHelper));
        Name = "testOutput";
        var testTag = !string.IsNullOrWhiteSpace(testName) ? $" :: {testName}" : string.Empty;
        Layout =
            $$"""
              [BEGIN ${date:format=HH\:mm\:ss.fffffff} ${level:format=Name:padding=-3:uppercase=true} ${logger}{{testTag}}]
              ${message}
              [END ${date:format=HH\:mm\:ss.fffffff} ${level:format=Name:padding=-3:uppercase=true} ${logger}{{testTag}}]${newline}
              """;
    }

    protected override void Write(LogEventInfo logEvent)
    {
        try
        {
            var logMessage = RenderLogEvent(Layout, logEvent);
            _testOutputHelper.WriteLine(logMessage);
        }
        catch (Exception ex)
        {
            // Avoid throwing exceptions from logging infrastructure in test scenarios.
            Debug.WriteLine($"Failed to write to test output: {ex.Message}");
        }
    }
}
