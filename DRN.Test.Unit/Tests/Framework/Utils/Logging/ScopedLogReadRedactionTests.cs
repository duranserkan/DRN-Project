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
    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public void Detached_Capture_Reads_And_Copies_Should_Enforce_Destination_Depth(bool copy)
    {
        var capture = Policy().Capture("Items", new object[] { new object[] { "deep-probe" } });
        var destination = new DiagnosticSanitizer(new DiagnosticLogOptions { MaxDepth = 1, MaxProcessingMilliseconds = 100 });

        var result = copy ? destination.CopyCapture(capture).Value
            : destination.MaterializeSnapshot(new Dictionary<string, object> { ["Items"] = capture.Value! }, true)["Items"];

        var items = JsonSerializer.SerializeToElement(result);
        items.GetArrayLength().Should().Be(1);
        items[0].GetArrayLength().Should().Be(1);
        items[0][0].GetString().Should().Be(DiagnosticSanitizer.Limited);
        JsonSerializer.Serialize(capture.Value).Should().Contain("deep-probe");
    }

    [Theory]
    [DataInlineUnit("TargetUrl", "/?first=one&second=two#first=three&second=four",
        "/?first=redacted&second=redacted#first=redacted&second=redacted")]
    [DataInlineUnit("Request_QueryString", "first=one&second=two", "first=redacted&second=redacted")]
    public void Deferred_Text_Reads_Should_Combine_Source_And_Destination_Rules(string path, string text, string expected)
    {
        var capture = Policy("first").Capture(path, text, deferRedaction: true);
        var snapshot = new Dictionary<string, object> { [path] = capture.Value! };

        Policy("second").MaterializeSnapshot(snapshot, true)[path].Should().Be(expected);
        Policy("second").MaterializeSnapshot(snapshot, false)[path].Should().Be(text);
    }

    [Theory]
    [DataInlineUnit("TargetUrl", "/?first=one&second=two&third=three&fourth=four", "/?")]
    [DataInlineUnit("Request_QueryString", "first=one&second=two&third=three&fourth=four", "")]
    public void Deferred_Reads_Should_Keep_Interleaved_Source_Policies_Separate(string path, string text, string prefix)
    {
        var first = Policy("first").Capture(path, text, deferRedaction: true);
        var second = Policy("second").Capture(path, text, deferRedaction: true);
        var third = Policy("third").Capture(path, text, deferRedaction: true);
        var snapshot = new Dictionary<string, object>
        {
            ["First"] = first.Value!, ["Third"] = third.Value!, ["Second"] = second.Value!,
            ["FirstAgain"] = first.Value!, ["ThirdAgain"] = third.Value!
        };
        var destination = Policy("fourth");

        for (var read = 0; read < 2; read++)
        {
            var output = destination.MaterializeSnapshot(snapshot, true);
            output["First"].Should().Be(prefix + "first=redacted&second=two&third=three&fourth=redacted");
            output["Third"].Should().Be(prefix + "first=one&second=two&third=redacted&fourth=redacted");
            output["Second"].Should().Be(prefix + "first=one&second=redacted&third=three&fourth=redacted");
            output["FirstAgain"].Should().Be(output["First"]);
            output["ThirdAgain"].Should().Be(output["Third"]);
        }
        destination.MaterializeSnapshot(snapshot, false).Values.Should().AllBeEquivalentTo(text);
    }

    [Fact]
    public void Source_Worker_Should_Reset_Only_Its_Per_Value_State()
    {
        var source = new DiagnosticSanitizer(new DiagnosticLogOptions
        {
            MaxNodes = 16, MaxFieldBytes = 1024, MaxInputBytes = 1024, MaxProcessingMilliseconds = 100,
            SensitiveFields = ["first"]
        });
        source.SanitizeText(new string('x', 1024)).Should().HaveLength(1024);
        source.SanitizeText("x").Should().Be(DiagnosticSanitizer.Limited);
        for (var read = 0; read < 2; read++)
        {
            source.RedactCapturedTextWithFreshBudget(DiagnosticRedactionKind.Url, "/?first=one&second=two")
                .Should().Be("/?first=redacted&second=two");
            source.GetStatus().Should().BeEmpty();
            for (var index = 0; index < 16; index++) source.SanitizeValue("Count", index).Should().Be(index);
            source.SanitizeValue("Count", 42).Should().Be(DiagnosticSanitizer.Limited);
        }
        source.RedactCapturedTextWithFreshBudget(DiagnosticRedactionKind.Url, "/?value=%25GG")
            .Should().Be(DiagnosticSanitizer.Invalid);
        source.RedactCapturedTextWithFreshBudget(DiagnosticRedactionKind.QueryString, "first=one&second=two")
            .Should().Be("first=redacted&second=two");
        source.GetStatus().Should().BeEmpty();
    }

    [Fact]
    public void Reused_Source_Workers_Should_Preserve_Limits_And_Key_Sequences()
    {
        var options = new DiagnosticLogOptions
        {
            MaxKeyBytes = 32, MaxCollectionEntries = 1, MaxProcessingMilliseconds = 100
        };
        var captured = new DiagnosticCapturedValue(new string('x', 33) + "=one&second=two",
            DiagnosticRedactionKind.QueryString, options);
        var source = new DiagnosticSanitizer(options);
        var first = source.RedactCapturedTextWithFreshBudget(captured.Kind, (string)captured.Value!);
        source.RedactCapturedTextWithFreshBudget(captured.Kind, (string)captured.Value!).Should().Be(first);
        var output = Policy().MaterializeSnapshot(new Dictionary<string, object>
        {
            ["First"] = captured, ["Second"] = captured
        }, true);

        // Destination keys remain unique across values even though each source sequence resets.
        output["First"].Should().NotBe(output["Second"]);
        foreach (var value in output.Values)
        {
            var text = (string)value;
            text.Should().StartWith("_diagnostic_field_").And.Contain("=redacted&")
                .And.NotContain("=one").And.NotContain("second=two");
            Uri.UnescapeDataString(text).Should().Contain(DiagnosticSanitizer.Limited);
        }
    }

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public void Deferred_Reads_Should_Keep_Destination_Budgets_Cumulative(bool nodeLimit)
    {
        var text = "first=" + new string('x', 600);
        var capture = Policy("first").Capture("Request_QueryString", text, deferRedaction: true);
        var destination = new DiagnosticSanitizer(new DiagnosticLogOptions
        {
            MaxNodes = 16, MaxFieldBytes = 1024, MaxInputBytes = 1024, MaxProcessingMilliseconds = 100
        });
        if (nodeLimit)
            for (var index = 0; index < 14; index++) destination.SanitizeValue("Count", index).Should().Be(index);

        var output = destination.MaterializeSnapshot(new Dictionary<string, object>
        {
            ["First"] = capture.Value!, ["Second"] = capture.Value!
        }, true);

        output["First"].Should().Be("first=redacted");
        output["Second"].Should().Be(DiagnosticSanitizer.Limited);
        destination.GetStatus().Should().ContainKey("DiagnosticCaptureWarnings");
    }

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public void Source_Workers_Should_Not_Survive_A_Materialization(bool fail)
    {
        var options = new DiagnosticLogOptions { SensitiveFields = ["first"], MaxProcessingMilliseconds = 100 };
        var captured = new DiagnosticCapturedValue("first=one&second=two", DiagnosticRedactionKind.QueryString, options);
        var snapshot = new Dictionary<string, object> { ["Value"] = captured };
        var destination = Policy();

        if (fail)
        {
            snapshot["Invalid"] = new DiagnosticCapturedValue("value=one", DiagnosticRedactionKind.QueryString,
                options with { MaxNodes = 0 });
            destination.MaterializeSnapshot(snapshot, true)["DiagnosticCapture"].Should().Be(DiagnosticSanitizer.Invalid);
            snapshot.Remove("Invalid");
        }
        else destination.MaterializeSnapshot(snapshot, true)["Value"].Should().Be("first=redacted&second=two");
        options.SensitiveFields[0] = "second";
        destination.MaterializeSnapshot(snapshot, true)["Value"].Should().Be("first=one&second=redacted");
    }

    [Fact]
    public void Failed_Snapshots_Should_Remain_Independent_And_Allow_Later_Reads()
    {
        var destination = Policy();
        var snapshot = new Dictionary<string, object>
        {
            ["Value"] = new DiagnosticCapturedValue("value=one", DiagnosticRedactionKind.QueryString,
                new DiagnosticLogOptions { MaxNodes = 0 })
        };

        var first = destination.MaterializeSnapshot(snapshot, true);
        ((IDictionary<string, object>)first)["DiagnosticCapture"] = "caller-edited";
        var second = destination.MaterializeSnapshot(snapshot, true);

        second.Should().NotBeSameAs(first);
        second["DiagnosticCapture"].Should().Be(DiagnosticSanitizer.Invalid);
        snapshot["Value"] = "visible";
        destination.MaterializeSnapshot(snapshot, true)["Value"].Should().Be("visible");
        destination.GetStatus()["DiagnosticCaptureWarnings"].Should().BeEquivalentTo(new[] { "SanitizationFailed" });
    }

    [Fact]
    public void Native_Copies_Should_Share_The_Destination_Capture_Budget()
    {
        var source = Policy().Capture("Items", new[] { 1 });
        var sanitizer = new DiagnosticSanitizer(new DiagnosticLogOptions { MaxNodes = 16, MaxProcessingMilliseconds = 100 });
        for (var index = 0; index < 15; index++) sanitizer.SanitizeValue("Count", index).Should().Be(index);

        var copy = sanitizer.CopyCapture(source);

        var items = JsonSerializer.SerializeToElement(copy.Value);
        items.GetArrayLength().Should().Be(1);
        items[0].GetString().Should().Be(DiagnosticSanitizer.Limited);
        sanitizer.SanitizeValue("Count", 42).Should().Be(DiagnosticSanitizer.Limited);
    }

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public void Deferred_Throwing_Getters_Should_Preserve_Classification_Without_Reexecution(bool configuredName)
    {
        using var settings = SettingsProvider.Development();
        var sanitizer = configuredName ? Policy("Model.Ordinary") : Policy();
        var source = new ScopedLog(settings, sanitizer);
        var model = new ThrowingCaptureModel();
        source.Add("Model", model);
        model.ReadCount.Should().Be(2);
        sanitizer.GetStatus()["DiagnosticCaptureWarnings"].Should().BeEquivalentTo(new[] { "GetterFailed" });

        var copy = new ScopedLog(settings, Policy());
        copy.CopyFrom(source);
        foreach (var log in new[] { source, copy })
        {
            var raw = JsonSerializer.SerializeToElement(log.GetLogs(false)["Model"]);
            raw.GetProperty("Classified").GetString().Should().Be(DiagnosticSanitizer.Invalid);
            raw.GetProperty("Ordinary").GetString().Should().Be(DiagnosticSanitizer.Invalid);
            var redacted = JsonSerializer.SerializeToElement(log.GetLogs()["Model"]);
            redacted.GetProperty("Classified").GetString().Should().Be(DiagnosticSanitizer.Redacted);
            redacted.GetProperty("Ordinary").GetString().Should().Be(configuredName
                ? DiagnosticSanitizer.Redacted : DiagnosticSanitizer.Invalid);
        }
        model.ReadCount.Should().Be(2);
    }

    [Fact]
    public void Top_Level_Throwing_Getters_Should_Record_Warnings_And_Preserve_Unavailable_Values()
    {
        using var settings = SettingsProvider.Development();
        var sanitizer = Policy();
        var log = new ScopedLog(settings, sanitizer);
        var model = new ThrowingCaptureModel();

        log.AddProperties("Model", model);

        sanitizer.GetStatus()["DiagnosticCaptureWarnings"].Should().BeEquivalentTo(new[] { "GetterFailed" });
        var raw = log.GetLogs(false);
        raw["Model.Classified"].Should().Be("[diagnostic getter unavailable]");
        raw["Model.Ordinary"].Should().Be("[diagnostic getter unavailable]");
        var redacted = log.GetLogs();
        redacted["Model.Classified"].Should().Be(DiagnosticSanitizer.Redacted);
        redacted["Model.Ordinary"].Should().Be("[diagnostic getter unavailable]");
        model.ReadCount.Should().Be(2);
    }

    private sealed class ThrowingCaptureModel
    {
        [IgnoreLog] public int ReadCount { get; private set; }
        [Sensitive] public string Classified { get { ReadCount++; throw new InvalidOperationException("Getter probe."); } }
        public string Ordinary { get { ReadCount++; throw new InvalidOperationException("Getter probe."); } }
    }

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
        log.Add("Map", new Dictionary<string, object> { ["First"] = 1, ["Second"] = 2, ["Third"] = 3 });
        log.Add("TargetUrl", "/?a=one&b=two&c=three");
        log.GetLogs()["Password"].Should().Be(DiagnosticSanitizer.Redacted);
        log.GetLogs(false)["Password"].Should().Be(DiagnosticSanitizer.Limited);
        log.GetLogs(false)["TargetUrl"].Should().Be(DiagnosticSanitizer.Limited);
        foreach (var redact in new[] { true, false })
        {
            var items = JsonSerializer.SerializeToElement(log.GetLogs(redact)["Items"]);
            items.GetArrayLength().Should().Be(3);
            items[2].GetString().Should().Be(DiagnosticSanitizer.Limited);
            var map = JsonSerializer.SerializeToElement(log.GetLogs(redact)["Map"]);
            map.EnumerateObject().Count().Should().Be(3);
            map.GetProperty("First").GetInt32().Should().Be(1);
            map.GetProperty("Second").GetInt32().Should().Be(2);
            map.GetProperty("_diagnostic_limit").GetString().Should().Be(DiagnosticSanitizer.Limited);
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
