using System.Text.Json;
using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Logging.Diagnostics;
using Microsoft.Extensions.Logging;
using static DRN.Test.Unit.Tests.Framework.Utils.Logging.DiagnosticTestSupport;

// These fixtures intentionally exercise runtime protection for sensitive capture names.
#pragma warning disable DRN0017, DRN0020

namespace DRN.Test.Unit.Tests.Framework.Utils.Logging;

public class ScopedLogReadRedactionTests
{
    [Fact]
    public void Materialized_Lists_And_Native_Copies_Should_Remain_Detached()
    {
        using var settings = SettingsProvider.Development();
        var source = new ScopedLog(settings, Policy());
        source.AddToList("Items", new CountedModel());
        var copy = new ScopedLog(settings, Policy());
        copy.CopyFrom(source);

        var raw = (IList<object?>)source.GetLogs(false)["Items"];
        ((IDictionary<string, object?>)raw[0]!)["Value"] = "caller-mutation";
        raw.Add("caller-item");
        source.AddToList("Items", "source-item");

        JsonSerializer.Serialize(source.GetLogs(false)).Should().Contain("classified-probe").And.Contain("source-item")
            .And.NotContain("caller-mutation").And.NotContain("caller-item");
        JsonSerializer.Serialize(copy.GetLogs(false)).Should().Contain("classified-probe").And.NotContain("source-item")
            .And.NotContain("caller-mutation").And.NotContain("caller-item");
        source.ToString().Should().NotContain("classified-probe");
        copy.ToString().Should().NotContain("classified-probe");
    }

    [Fact]
    public void Reads_Should_Preserve_Classification_Without_Recapturing_Values_Or_Leaking_To_Logs()
    {
        using var settings = SettingsProvider.Development();
        var log = new ScopedLog(settings, Policy());
        var model = new CountedModel();
        log.AddProperties("Flat", model);
        log.Add("Nested", model);
        log.AddToList("Items", model);
        log.AddDictionary("Fields", new Dictionary<string, string> { ["Value"] = "dictionary-probe" },
            new DiagnosticKeyClassification("Value", DiagnosticDataKind.Sensitive));
        IScopedUserSummary user = new ScopedUserSummary
        {
            ClaimsByType = new Dictionary<string, ClaimGroupSummary> { ["custom"] = new() { Value = "claim-probe" } }
        };
        log.AddProperties("User", user);
        model.ReadCount.Should().Be(3);
        log.GetLogs()["Flat.Ignored"].Should().Be(ScopedLogConventions.IgnoredLogValue);
        model.Text = "mutation-probe";

        for (var read = 0; read < 2; read++)
        {
            var raw = JsonSerializer.Serialize(log.GetLogs(redact: false));
            raw.Should().Contain("classified-probe").And.Contain("dictionary-probe").And.Contain("claim-probe")
                .And.NotContain("mutation-probe");
            log.ToString().Should().NotContain("classified-probe").And.NotContain("dictionary-probe").And.NotContain("claim-probe");
        }
        model.ReadCount.Should().Be(3);
        var rawNested = (IDictionary<string, object>)log.GetLogs(false)["Nested"];
        rawNested["Value"] = "snapshot-mutation";
        JsonSerializer.Serialize(log.GetLogs(false)).Should().NotContain("snapshot-mutation");

        var logger = Substitute.For<ILogger>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        logger.LogScoped(log);
        var emissions = logger.ReceivedCalls().Where(call => call.GetMethodInfo().Name == "Log").ToArray();
        emissions.Should().NotBeEmpty();
        foreach (var emission in emissions)
            emission.GetArguments()[2]!.ToString().Should().NotContain("classified-probe")
                .And.NotContain("dictionary-probe").And.NotContain("claim-probe");
    }

    [Theory]
    [DataInlineUnit("Staging")]
    [DataInlineUnit("Production")]
    public void Unredacted_Reads_Should_Be_Rejected_Outside_Development(string environment)
    {
        using var settings = DiagnosticTestSupport.Settings(environment);
        var log = new ScopedLog(settings, Policy());
        log.Add("Password", "private-probe");
        log.GetLogs()["Password"].Should().Be(DiagnosticSanitizer.Redacted);
        Action read = () => log.GetLogs(redact: false);
        read.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Copies_Should_Preserve_Classification_And_Source_Url_Protection()
    {
        using var settings = SettingsProvider.Development();
        var source = new ScopedLog(settings, new DiagnosticSanitizer(new DiagnosticLogOptions
        {
            SensitiveFields = ["opaque"], SensitivePathSegments = ["handoff"], MaxProcessingMilliseconds = 100
        }));
        source.Add("Model", new CountedModel());
        source.Add("TargetUrl", "/handoff/path-probe?opaque=query-probe");
        var copy = new ScopedLog(settings, Policy());
        copy.CopyFrom(source);
        copy.ToString().Should().NotContain("classified-probe").And.NotContain("path-probe").And.NotContain("query-probe");
        JsonSerializer.Serialize(copy.GetLogs(false)).Should().Contain("classified-probe").And.Contain("path-probe").And.Contain("query-probe");
    }

    [Fact]
    public void Both_Views_Should_Keep_Capture_Limits_And_IgnoreLog()
    {
        using var settings = SettingsProvider.Development();
        var log = new ScopedLog(settings, new DiagnosticSanitizer(new DiagnosticLogOptions
        {
            MaxFieldBytes = 1024, MaxCollectionEntries = 2, MaxProcessingMilliseconds = 100
        }));
        log.Add("Password", new string('x', 1025));
        log.Add("Items", new[] { "one", "two", "three" });
        log.Add("TargetUrl", "/?a=one&b=two&c=three");
        log.GetLogs()["Password"].Should().Be(DiagnosticSanitizer.Redacted);
        log.GetLogs(false)["Password"].Should().Be(DiagnosticSanitizer.Limited);
        log.GetLogs(false)["TargetUrl"].Should().Be(DiagnosticSanitizer.Limited);
        foreach (var redact in new[] { true, false })
        {
            var items = JsonSerializer.SerializeToElement(log.GetLogs(redact)["Items"]);
            items.GetArrayLength().Should().Be(3);
            items[2].GetString().Should().Be(DiagnosticSanitizer.Limited);
        }
    }

    [Theory]
    [DataInlineUnit("Development")]
    [DataInlineUnit("Production")]
    public void Copies_To_Custom_Sanitizers_Should_Expose_Only_Redacted_Source_Values(string environment)
    {
        using var settings = DiagnosticTestSupport.Settings(environment);
        var source = new ScopedLog(settings, Policy());
        source.Add("Model", new CountedModel());
        source.AddToList("Items", new CountedModel());
        source.AddDictionary("Fields", new Dictionary<string, string> { ["Value"] = "dictionary-probe" },
            new DiagnosticKeyClassification("Value", DiagnosticDataKind.Sensitive));
        source.Add("TargetUrl", "/token/path-probe?token=query-probe");

        // A custom implementation cannot interpret native internal capture metadata.
        var sanitizer = Substitute.For<IDiagnosticSanitizer>();
        sanitizer.Options.Returns(new DiagnosticLogOptions());
        sanitizer.GetStatus().Returns(new Dictionary<string, object>());
        sanitizer.SanitizeKey(Arg.Any<string>()).Returns(call => call.Arg<string>());
        sanitizer.SanitizeValue(Arg.Any<string>(), Arg.Any<object>()).Returns(call => call[1]);
        var destination = new ScopedLog(settings, sanitizer);
        destination.CopyFrom(source);

        var inputs = sanitizer.ReceivedCalls().Where(call => call.GetMethodInfo().Name == "SanitizeValue")
            .Select(call => call.GetArguments()[1]).ToArray();
        foreach (var json in new[] { JsonSerializer.Serialize(inputs), destination.ToString() })
            json.Should().NotContain("classified-probe").And.NotContain("dictionary-probe")
                .And.NotContain("path-probe").And.NotContain("query-probe")
                .And.Contain(DiagnosticSanitizer.Redacted);
        destination.GetLogs()["TargetUrl"].Should().Be("/token/redacted?token=redacted");
    }

    [Fact]
    public void Rejected_Replacement_Should_Remove_Previous_Capture_And_Release_Its_Budget()
    {
        using var settings = SettingsProvider.Development();
        var log = new ScopedLog(settings, new DiagnosticSanitizer(new DiagnosticLogOptions
        {
            MaxScopeBytes = 8192, MaxProcessingMilliseconds = 100
        }));
        var value = new string('x', 2500);
        log.Add("First", value);
        log.Add("Second", value);
        log.GetLogs().Should().ContainKey("First").And.ContainKey("Second");

        log.Add("First", new string('x', 8192));
        log.Add("NewOversized", new string('x', 8192));
        log.Add("Third", value);

        foreach (var redact in new[] { true, false })
        {
            var snapshot = log.GetLogs(redact);
            snapshot.Should().NotContainKey("First").And.NotContainKey("NewOversized");
            snapshot["Second"].Should().Be(value);
            snapshot["Third"].Should().Be(value);
            snapshot["DiagnosticScopeLimit"].Should().Be(true);
        }
        var copy = new ScopedLog(settings, Policy());
        copy.CopyFrom(log);
        copy.GetLogs().Should().NotContainKey("First");
        copy.GetLogs()["Third"].Should().Be(value);
    }

    private sealed class CountedModel
    {
        [IgnoreLog] public string Text { get; set; } = "classified-probe";
        [IgnoreLog] public int ReadCount { get; private set; }
        [Sensitive] public string Value { get { ReadCount++; return Text; } }
        [IgnoreLog] public string Ignored => throw new InvalidOperationException("Must not execute");
    }
}
