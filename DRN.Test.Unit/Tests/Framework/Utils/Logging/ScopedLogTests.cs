using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Logging.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DRN.Test.Unit.Tests.Framework.Utils.Logging;

public class ScopedLogTests
{
    [Theory]
    [DataInlineUnit("none")]
    [DataInlineUnit("field")]
    [DataInlineUnit("path")]
    public void Metric_Identifiers_Should_Ignore_Runtime_Name_Rules(string rule)
    {
        using var settings = SettingsProvider.Development();
        var log = new ScopedLog(settings, new DiagnosticSanitizer(MetricOptions(rule)));
        log.Increase("TokenWork").Should().Be(1);
        log.IncreaseTimeSpentOn("TokenWork", TimeSpan.FromSeconds(2))
            .Should().Be(TimeSpan.FromSeconds(2));
        using (log.Measure("TokenWork")) { }
        var snapshot = log.GetLogs();
        snapshot.Should().ContainKey("Stats_TokenWork");
        snapshot.Should().ContainKey(ScopedLogConventions.TimeSpentOnKey("TokenWork"));
        snapshot[ScopedLogConventions.TimeSpentOnCounter("TokenWork")].Should().Be(2L);
    }

    [Fact]
    public void Metrics_Should_Keep_Foreign_Sanitizer_Sensitivity_Checks()
    {
        using var settings = SettingsProvider.Development();
        var sanitizer = Substitute.For<IDiagnosticSanitizer>();
        sanitizer.Options.Returns(new DiagnosticLogOptions());
        sanitizer.SanitizeKey(Arg.Any<string>()).Returns(call => call.Arg<string>());
        sanitizer.IsSensitive("Stats_ClientWork").Returns(true);
        var log = new ScopedLog(settings, sanitizer);
        log.Increase("ClientWork").Should().Be(0);
        log.IncreaseTimeSpentOn("ClientWork", TimeSpan.FromSeconds(2)).Should().Be(TimeSpan.Zero);
        using (log.Measure("ClientWork")) { }
        sanitizer.Received(2).IsSensitive("Stats_ClientWork");
    }

    [Theory]
    [DataInlineUnit("none")]
    [DataInlineUnit("field")]
    [DataInlineUnit("path")]
    public void Metric_Copies_Should_Preserve_Values_Without_Destination_Name_Matching(string rule)
    {
        using var settings = SettingsProvider.Development();
        var options = MetricOptions("none");
        var source = new ScopedLog(settings, new DiagnosticSanitizer(options));
        source.Increase("TokenWork", 4);
        source.Increase("TokenWork", 0);
        source.IncreaseTimeSpentOn("TokenWork", TimeSpan.FromSeconds(2));
        source.IncreaseTimeSpentOn("TokenWork", TimeSpan.Zero);
        var first = new ScopedLog(settings, new DiagnosticSanitizer(options));
        first.CopyFrom(source);
        first.Increase("TokenWork").Should().Be(5);
        first.IncreaseTimeSpentOn("TokenWork", TimeSpan.FromSeconds(1)).Should().Be(TimeSpan.FromSeconds(3));

        var destination = new ScopedLog(settings, new DiagnosticSanitizer(MetricOptions(rule)));
        destination.CopyFrom(first);
        var snapshot = destination.GetLogs();
        var counterKey = ScopedLogConventions.TimeSpentOnCounter("TokenWork");
        var durationKey = ScopedLogConventions.TimeSpentOnKey("TokenWork");
        snapshot["Stats_TokenWork"].Should().Be(5L);
        snapshot[counterKey].Should().Be(3L);
        snapshot[durationKey].Should().Be(3d);
        destination.Increase("TokenWork").Should().Be(6);
        destination.IncreaseTimeSpentOn("TokenWork", TimeSpan.FromSeconds(1)).Should().Be(TimeSpan.FromSeconds(4));
        source.GetLogs()["Stats_TokenWork"].Should().Be(4L);
        source.GetLogs()[durationKey].Should().Be(2d);

        // DiagnosticNameExemption: Stats_TokenWork | Deliberate replacement fixture for a metric key.
        first.Add("Stats_TokenWork", "replacement-probe");
        destination.CopyFrom(first);
        destination.GetLogs()["Stats_TokenWork"].Should().Be("replacement-probe");
    }

    private static DiagnosticLogOptions MetricOptions(string rule) => new()
    {
        SensitiveFields = rule == "field" ? ["TokenWork"] : [],
        SensitiveFieldPaths = rule == "path" ? ["Stats_TokenWork"] : [],
        MaxProcessingMilliseconds = 100
    };

    [Theory]
    [DataInlineUnit("en-US")]
    [DataInlineUnit("tr-TR")]
    public void Snapshots_And_Copies_Should_Preserve_Ordinal_Keys_Regardless_Of_Culture(string cultureName)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            var source = CreateLog();
            source.Add("Field.\u00e9", "composed");
            source.Add("Field.z", "lowercase");
            source.Add("Field.e\u0301", "decomposed");
            source.Add("Field.Z", "uppercase");

            var destination = CreateLog();
            destination.CopyFrom(source);

            foreach (var snapshot in new[] { source.GetLogs(), destination.GetLogs() })
            {
                snapshot.Keys.Where(key => key.StartsWith("Field.", StringComparison.Ordinal))
                    .Should().Equal("Field.Z", "Field.e\u0301", "Field.z", "Field.\u00e9");
                snapshot["Field.\u00e9"].Should().Be("composed");
                snapshot["Field.e\u0301"].Should().Be("decomposed");
                snapshot["Field.Z"].Should().Be("uppercase");
                snapshot["Field.z"].Should().Be("lowercase");
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void GetLogs_Should_Detach_Actions_And_Additional_Events()
    {
        var log = CreateLog();
        var first = new ScopeEvent(new EventId(1, "First"));
        var second = new ScopeEvent(new EventId(2, "Second"));
        log.AddToActions("before");
        log.WithEvent(first).WithEvent(second);
        var snapshot = log.GetLogs();

        log.AddToActions("after");
        log.WithEvent(new ScopeEvent(new EventId(3, "Third")));

        ((List<object>)snapshot["Actions"]).Should().Equal("before");
        ((List<object>)snapshot["AdditionalEvents"]).Should().Equal(second);
        ((List<object>)snapshot["Actions"]).Add("snapshot-only");
        ((List<object>)log.GetLogs()["Actions"]).Should().Equal("before", "after");
    }

    [Fact]
    public void AddProperties_Should_Skip_Dictionary_Indexers()
    {
        var log = CreateLog();

        log.AddProperties("Map", new Dictionary<string, string> { ["entry"] = "value" });

        var snapshot = log.GetLogs();
        snapshot["Map.Count"].Should().Be(1);
        snapshot.Should().NotContainKey("Map.Item");
    }

    [Fact]
    public void AddProperties_Should_Use_Only_Public_Instance_Getters_And_Honor_Ignored_Properties()
    {
        var log = CreateLog();

        log.AddProperties("Data", new PropertyLogData(), nameof(PropertyLogData.Ignored));

        var snapshot = log.GetLogs();
        snapshot["Data.Visible"].Should().Be("visible");
        snapshot["Data.Ignored"].Should().Be(ScopedLogConventions.IgnoredLogValue);
        snapshot.Should().NotContainKey("Data.Hidden");
        snapshot.Should().NotContainKey("Data.Static");
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private sealed class PropertyLogData
    {
        [SuppressMessage("Performance", "CA1822", Justification = "Exercises capture of a public instance getter.")]
        public string Visible => "visible";
        public string Hidden { private get; set; } = "hidden";
        public static string Static => "static";
        [SuppressMessage("Performance", "CA1822", Justification = "Verifies that ignoring an instance property by name skips its getter.")]
        public string Ignored => throw new InvalidOperationException("Ignored getters must not execute.");
    }

    [Fact]
    public void WithEvent_Should_Preserve_Primary_And_Retain_Additional_Events()
    {
        var log = CreateLog();
        var primary = new ScopeEvent(new EventId(1, "Primary"), "success", "completed");
        var additional = new ScopeEvent(new EventId(2, "Secondary"), "denied", "policy");

        log.WithEvent(primary).WithEvent(additional);

        log.Event.Should().Be(primary);
        log.EventId.Should().Be(1);
        log.EventName.Should().Be("Primary");
        log.EventOutcome.Should().Be("success");
        log.EventReason.Should().Be("completed");
        var data = log.GetLogs();
        data["EventId"].Should().Be(1);
        data["EventName"].Should().Be("Primary");
        data["EventOutcome"].Should().Be("success");
        data["EventReason"].Should().Be("completed");
        ((IEnumerable<object>)data["AdditionalEvents"]).Should().Equal(additional);
    }

    [Fact]
    public void Events_Should_Retain_Eight_Additional_Events_And_Report_Only_Omissions()
    {
        var log = CreateLog();
        var primary = new ScopeEvent(new EventId(0, "Primary"));
        log.GetLogs().Should().NotContainKey("OmittedEventCount");
        log.WithEvent(primary);
        log.GetLogs().Should().NotContainKey("OmittedEventCount");
        var additional = Enumerable.Range(1, 8).Select(id => new ScopeEvent(new EventId(id))).ToArray();
        foreach (var item in additional) log.WithEvent(item);
        var full = log.GetLogs();
        full.Should().NotContainKey("OmittedEventCount");

        log.WithEvent(new ScopeEvent(new EventId(9)));
        log.AddToList("AdditionalEvents", new ScopeEvent(new EventId(10)));
        log.Event.Should().BeSameAs(primary);
        ((List<object>)log.GetLogs()["AdditionalEvents"]).Should().Equal(additional);
        log.GetLogs()["OmittedEventCount"].Should().Be(2L);
        full.Should().NotContainKey("OmittedEventCount");

        var copy = CreateLog();
        copy.CopyFrom(log);
        copy.Event.Should().BeSameAs(primary);
        ((List<object>)copy.GetLogs()["AdditionalEvents"]).Should().Equal(additional);
        copy.GetLogs()["OmittedEventCount"].Should().Be(2L);
        copy.CopyFrom(copy);
        copy.GetLogs()["OmittedEventCount"].Should().Be(2L);

        var occupied = CreateLog();
        occupied.WithEvent(new ScopeEvent(new EventId(100)));
        occupied.CopyFrom(log);
        ((List<object>)occupied.GetLogs()["AdditionalEvents"]).Should().Equal(new[] { primary }.Concat(additional.Take(7)));
        occupied.GetLogs()["OmittedEventCount"].Should().Be(3L);
    }

    [Fact]
    public void Additional_Event_Limit_Should_Preserve_Primary_Capacity_And_Count_Concurrent_Omissions()
    {
        var log = CreateLog();
        Parallel.For(0, 32, id => log.AddToList("AdditionalEvents", new ScopeEvent(new EventId(id))));
        log.Event.Should().BeNull();
        var primary = new ScopeEvent(new EventId(100));
        log.WithEvent(primary);
        log.Event.Should().BeSameAs(primary);
        ((List<object>)log.GetLogs()["AdditionalEvents"]).Should().HaveCount(8);
        log.GetLogs()["OmittedEventCount"].Should().Be(24L);
    }

    [Fact]
    public void Foreign_Event_Omissions_Should_Be_Bounded_Copied_And_Emitted()
    {
        var events = Enumerable.Range(1, 10).Select(id => new ScopeEvent(new EventId(id))).ToArray();
        var source = Substitute.For<IScopedLog>();
        source.Event.Returns(new ScopeEvent(new EventId(0)));
        source.GetLogs().Returns(new Dictionary<string, object>
        {
            ["AdditionalEvents"] = events,
            ["OmittedEventCount"] = 3L
        });
        var copy = CreateLog();
        copy.CopyFrom(source);
        ((List<object>)copy.GetLogs()["AdditionalEvents"]).Should().Equal(events.Take(8));
        copy.GetLogs()["OmittedEventCount"].Should().Be(5L);

        foreach (var log in new IScopedLog[] { source, copy })
        {
            var logger = Substitute.For<ILogger>();
            logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
            logger.LogScoped(log);
            var call = logger.ReceivedCalls().Single(item => item.GetMethodInfo().Name == "Log").GetArguments();
            var state = (IReadOnlyList<KeyValuePair<string, object?>>)call[2]!;
            var record = (IReadOnlyDictionary<string, object>)state.Single(pair => pair.Key == "@Logs").Value!;
            ((List<object>)record["AdditionalEvents"]).Should().Equal(events.Take(8));
            record["OmittedEventCount"].Should().Be(5L);
        }
    }

    [Fact]
    public void Events_Should_Bypass_Diagnostic_Budgets_And_Remain_Detached_On_Copy()
    {
        using var settings = SettingsProvider.Development();
        var options = new DiagnosticLogOptions
        {
            MaxFieldBytes = 1024, MaxInputBytes = 1024, MaxScopeBytes = 4096,
            MaxNodes = 16, MaxCollectionEntries = 1, MaxProcessingMilliseconds = 100
        };
        var sanitizer = new DiagnosticSanitizer(options);
        var source = new ScopedLog(settings, sanitizer);
        sanitizer.SanitizeText(new string('x', 1024));
        sanitizer.SanitizeText("overflow").Should().Be(DiagnosticSanitizer.Limited);
        var text = new string('e', 8192);
        var primary = new ScopeEvent(new EventId(42, text), text, text);
        var second = new ScopeEvent(new EventId(43, text), text, text);
        var third = new ScopeEvent(new EventId(44, text), text, text);
        source.WithEvent(primary).WithEvent(second).WithEvent(third);

        source.Event.Should().BeSameAs(primary);
        var snapshot = source.GetLogs();
        snapshot["EventName"].Should().Be(text);
        snapshot["EventOutcome"].Should().Be(text);
        snapshot["EventReason"].Should().Be(text);
        ((List<object>)snapshot["AdditionalEvents"]).Should().Equal(second, third);

        var destination = new ScopedLog(settings, new DiagnosticSanitizer(options));
        var destinationEvent = new ScopeEvent(new EventId(1, "Destination"));
        destination.WithEvent(destinationEvent);
        destination.CopyFrom(source);
        destination.Event.Should().BeSameAs(destinationEvent);
        ((List<object>)destination.GetLogs()["AdditionalEvents"]).Should().Equal(primary, second, third);
        ((List<object>)snapshot["AdditionalEvents"]).Clear();
        ((List<object>)source.GetLogs()["AdditionalEvents"]).Should().Equal(second, third);
        source.WithEvent(new ScopeEvent(new EventId(45, "Later")));
        ((List<object>)destination.GetLogs()["AdditionalEvents"]).Should().Equal(primary, second, third);

        var custom = CreateStorageLog(options);
        custom.CopyFrom(source);
        custom.Event.Should().BeSameAs(primary);
        ((List<object>)custom.GetLogs()["AdditionalEvents"]).Should().HaveCount(3);
    }

    [Fact]
    public void Events_Should_Bypass_Custom_Sanitizers_At_Capture_And_Emission()
    {
        var options = new DiagnosticLogOptions { MaxScopeBytes = 4096, MaxRecordBytes = 4096 };
        var log = CreateStorageLog(options);
        var text = new string('e', 8192);
        var primary = new ScopeEvent(new EventId(42, text), text, text);
        var additional = new ScopeEvent(new EventId(43, text), text, text);
        log.WithEvent(primary).WithEvent(additional);
        log.Sanitizer.DidNotReceive().SanitizeValue(Arg.Any<string>(), Arg.Is<object>(value => value is ScopeEvent));

        var policy = Substitute.For<IDiagnosticSanitizer>();
        policy.Options.Returns(options);
        policy.SanitizeValue("", Arg.Any<object>()).Returns(call =>
        {
            var diagnostics = (IReadOnlyDictionary<string, object>)call[1];
            diagnostics.Should().NotContainKey("EventId").And.NotContainKey("AdditionalEvents");
            return new Dictionary<string, object> { ["Safe"] = true };
        });
        var logger = Substitute.For<ILogger>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        logger.LogScoped(log, LogLevel.Warning, policy);
        var call = logger.ReceivedCalls().Single(item => item.GetMethodInfo().Name == "Log").GetArguments();
        ((EventId)call[1]!).Name.Should().Be(text);
        var state = (IReadOnlyList<KeyValuePair<string, object?>>)call[2]!;
        var record = (IReadOnlyDictionary<string, object>)state.Single(pair => pair.Key == "@Logs").Value!;
        record["Safe"].Should().Be(true);
        record.Should().NotContainKey("OmittedEventCount");
        record["EventName"].Should().Be(text);
        record["EventOutcome"].Should().Be(text);
        record["EventReason"].Should().Be(text);
        ((List<object>)record["AdditionalEvents"]).Should().Equal(additional);
    }

    [Fact]
    public void Foreign_Typed_Events_And_Unclassified_Fields_Should_Retain_Their_Values()
    {
        var text = new string('e', 8192);
        var primary = new ScopeEvent(new EventId(42, text), text, text);
        var additional = new ScopeEvent(new EventId(43, text), text, text);
        var source = Substitute.For<IScopedLog>();
        source.Event.Returns(primary);
        source.GetLogs().Returns(new Dictionary<string, object>
        {
            ["Password"] = "private-probe", ["EventId"] = 42, ["EventName"] = text,
            ["EventOutcome"] = text, ["EventReason"] = text,
            ["AdditionalEvents"] = new List<object> { additional }
        });
        var logger = Substitute.For<ILogger>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        logger.LogScoped(source, LogLevel.Information, new DiagnosticSanitizer(new DiagnosticLogOptions
        {
            MaxScopeBytes = 4096, MaxRecordBytes = 4096, MaxProcessingMilliseconds = 100
        }));
        var call = logger.ReceivedCalls().Single(item => item.GetMethodInfo().Name == "Log").GetArguments();
        var state = (IReadOnlyList<KeyValuePair<string, object?>>)call[2]!;
        var record = (IReadOnlyDictionary<string, object>)state.Single(pair => pair.Key == "@Logs").Value!;
        record["Password"].Should().Be("private-probe");
        record["EventReason"].Should().Be(text);
        ((List<object>)record["AdditionalEvents"]).Should().Equal(additional);

        var destination = CreateLog();
        destination.CopyFrom(source);
        destination.Event.Should().BeSameAs(primary);
        destination.GetLogs()["Password"].Should().Be("private-probe");
        ((List<object>)destination.GetLogs()["AdditionalEvents"]).Should().Equal(additional);
    }

    [Fact]
    public void Foreign_Event_Shaped_Diagnostics_Should_Not_Gain_Typed_Event_Authority()
    {
        var source = Substitute.For<IScopedLog>();
        source.GetLogs().Returns(new Dictionary<string, object>
        {
            ["AdditionalEvents"] = new List<object>
            {
                new Dictionary<string, object> { ["Password"] = "private-probe" }
            }
        });
        var logger = Substitute.For<ILogger>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        logger.LogScoped(source, LogLevel.Information, new DiagnosticSanitizer(new DiagnosticLogOptions
        {
            MaxProcessingMilliseconds = 100
        }));
        var call = logger.ReceivedCalls().Single(item => item.GetMethodInfo().Name == "Log").GetArguments();
        call[2]!.ToString().Should().Contain("private-probe");
    }

    [Fact]
    public void TraceId_Should_Capture_Activity_And_Remain_Stable()
    {
        using var activity = new Activity("scope-test").SetIdFormat(ActivityIdFormat.W3C).Start();
        var log = CreateLog();
        var traceId = activity.TraceId.ToString();
        activity.Stop();
        log.WithTraceIdentifier("http-request");

        log.TraceId.Should().Be(traceId);
        log.GetLogs()["TraceId"].Should().Be(traceId);
        log.GetLogs()["TraceIdentifier"].Should().Be("http-request");
    }

    [Fact]
    public void Untraced_Scopes_Should_Have_Correlation_Without_Fabricated_TraceIds()
    {
        var previous = Activity.Current;
        try
        {
            Activity.Current = null;
            var first = CreateLog();
            var second = CreateLog();
            first.TraceId.Should().BeNull();
            first.CorrelationId.Should().HaveLength(32).And.NotBe(second.CorrelationId);
            first.GetLogs()["CorrelationId"].Should().Be(first.CorrelationId);
            first.GetLogs().Should().NotContainKey("TraceId");

            using var activity = new Activity("later-trace").SetIdFormat(ActivityIdFormat.W3C).Start();
            var traced = CreateLog();
            var correlationId = first.CorrelationId;
            first.CopyFrom(traced);
            first.TraceId.Should().BeNull();
            first.CorrelationId.Should().Be(correlationId);
            first.GetLogs().Should().NotContainKey("TraceId");
        }
        finally
        {
            Activity.Current = previous;
        }
    }

    [Fact]
    public void LogScoped_Should_Forward_EventId_And_Preserve_Severity()
    {
        var logger = Substitute.For<ILogger>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        var log = CreateLog();
        var eventId = new EventId(1, "Primary");
        log.WithEvent(new ScopeEvent(eventId));
        logger.LogScoped(log);
        log.AddWarning("warning");
        logger.LogScoped(log);
        log.AddException(new InvalidOperationException("failure"));
        logger.LogScoped(log);

        var calls = logger.ReceivedCalls().Where(call => call.GetMethodInfo().Name == "Log")
            .Select(call => call.GetArguments()).ToArray();
        calls.Should().HaveCount(3);
        calls.Select(args => (LogLevel)args[0]!).Should().Equal(LogLevel.Information, LogLevel.Warning, LogLevel.Error);
        calls.Select(args => (EventId)args[1]!).Should().OnlyContain(id => id.Id == 1 && id.Name == "Primary");
        var state = (IReadOnlyList<KeyValuePair<string, object?>>)calls[0][2]!;
        state.Should().Contain(pair => pair.Key == "{OriginalFormat}" && Equals(pair.Value, "{@Logs}"));
        state.Should().Contain(pair => pair.Key == "@Logs" && pair.Value is IReadOnlyDictionary<string, object>);
    }

    [Fact]
    public void LogScoped_Without_Event_Should_Keep_Default_Id()
    {
        var logger = Substitute.For<ILogger>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        var log = CreateLog();

        logger.LogScoped(log);

        log.Event.Should().BeNull();
        log.GetLogs().Should().NotContainKey("EventId");
        var call = logger.ReceivedCalls().Single(call => call.GetMethodInfo().Name == "Log");
        ((EventId)call.GetArguments()[1]!).Should().Be(default(EventId));
    }

    [Fact]
    public void CopyFrom_Should_Preserve_Destination_State_And_Clone_Source_Entries()
    {
        var destination = CreateLog();
        var primary = new ScopeEvent(new EventId(1, "Primary"));
        var sourceEvent = new ScopeEvent(new EventId(2, "Source"));
        var source = CreateLog();
        destination.WithEvent(primary);
        source.WithEvent(sourceEvent);
        // DiagnosticNameExemption: DestinationKey | Fixture entry for destination retention.
        destination.Add("DestinationKey", "destination");
        destination.AddWarning("destination-warning");
        // DiagnosticNameExemption: SourceKey | Fixture entry for copy retention.
        source.Add("SourceKey", "source");
        source.AddException(new InvalidOperationException("source-exception"));
        source.AddToList("Items", "one");
        var traceId = destination.TraceId;

        destination.CopyFrom(source);
        source.AddToList("Items", "two");

        destination.Event.Should().Be(primary);
        destination.TraceId.Should().Be(traceId);
        ((IEnumerable<object>)destination.GetLogs()["AdditionalEvents"]).Should().Equal(sourceEvent);
        var logs = destination.GetLogs();
        logs.Should().ContainKey("DestinationKey");
        logs.Should().ContainKey("SourceKey");
        destination.HasWarning.Should().BeTrue();
        destination.HasException.Should().BeTrue();
        destination.GetLogs()["Items"].Should().BeAssignableTo<List<object>>()
            .Subject.Should().Equal("one");
    }

    [Fact]
    public void LogScoped_Should_Not_Snapshot_Disabled_Levels()
    {
        var logger = Substitute.For<ILogger>();
        var log = Substitute.For<IScopedLog>();

        logger.LogScoped(log);
        log.HasWarning.Returns(true);
        logger.LogScoped(log);
        log.HasException.Returns(true);
        logger.LogScoped(log);

        log.DidNotReceive().GetLogs();
        logger.Received(1).IsEnabled(LogLevel.Information);
        logger.Received(1).IsEnabled(LogLevel.Warning);
        logger.Received(1).IsEnabled(LogLevel.Error);
        logger.ReceivedCalls().Should().NotContain(call => call.GetMethodInfo().Name == "Log");
    }

    [Fact]
    public void Metrics_Should_Preserve_Primitive_Snapshots_And_Copy_Independence()
    {
        var source = CreateLog();
        source.Increase("Items", 4).Should().Be(4);
        source.IncreaseTimeSpentOn("Work", TimeSpan.FromSeconds(2)).Should().Be(TimeSpan.FromSeconds(2));
        var snapshot = source.GetLogs();
        var destination = CreateLog();
        destination.CopyFrom(source);

        source.Increase("Items", 3);
        source.IncreaseTimeSpentOn("Work", TimeSpan.FromSeconds(4));
        snapshot["Stats_Items"].Should().Be(4L);
        snapshot["Stats_TimeSpentOn_Work_Counter"].Should().Be(1L);
        snapshot["Stats_TimeSpentOn_Work_Seconds"].Should().Be(2d);
        destination.Increase("Items").Should().Be(5);
        destination.GetLogs()["Stats_TimeSpentOn_Work_Seconds"].Should().Be(2d);
        destination.IncreaseTimeSpentOn("Work", TimeSpan.FromSeconds(1)).Should().Be(TimeSpan.FromSeconds(3));
        source.GetLogs()["Stats_Items"].Should().Be(7L);
        source.GetLogs()["Stats_TimeSpentOn_Work_Seconds"].Should().Be(6d);
    }

    [Fact]
    public void Metrics_Should_Honor_Overwrites_Prefixes_And_Key_Collisions()
    {
        var log = CreateLog();
        log.Add("Stats_Items", 10L);
        log.Increase("Items", -2).Should().Be(8);
        log.Increase("Stats_Items", 1, "").Should().Be(9);
        log.Add("Stats_Items", "reset");
        log.Increase("Items").Should().Be(1);
        log.Add("Stats_Items", 5L);
        log.Increase("Items").Should().Be(6);
        log.Increase("bc", 2, "a").Should().Be(2);
        log.Increase("c", 3, "ab").Should().Be(5);

        log.Add("Custom_TimeSpentOn_Work_Seconds", 10d);
        log.GetLogs()["Custom_TimeSpentOn_Work_Seconds"].Should().Be(10d);
        log.IncreaseTimeSpentOn("Work", TimeSpan.FromSeconds(2), "Custom_").Should().Be(TimeSpan.FromSeconds(12));
        log.Add("Custom_TimeSpentOn_Work_Seconds", "reset");
        log.IncreaseTimeSpentOn("Work", TimeSpan.FromSeconds(3), "Custom_").Should().Be(TimeSpan.FromSeconds(3));
        log.Add("Custom_TimeSpentOn_Work_Seconds", 1d);
        log.IncreaseTimeSpentOn("Work", TimeSpan.FromSeconds(2), "Custom_").Should().Be(TimeSpan.FromSeconds(3));
        log.GetLogs()["Custom_TimeSpentOn_Work_Counter"].Should().Be(3L);
    }

    [Fact]
    public void Rejected_Replacements_Should_Release_Entry_Capacity_And_Byte_Accounting()
    {
        var log = CreateStorageLog(new DiagnosticLogOptions { MaxNodes = 16, MaxScopeBytes = 4096 });
        for (var index = 0; index < 8; index++) log.Store($"Slot{index}", 0L);
        log.Store("Overflow", 0L);
        log.GetLogs().Should().NotContainKey("Overflow");

        log.Store("Slot0", new string('x', 4096));
        log.GetLogs().Should().NotContainKey("Slot0");
        log.Store("Overflow", 0L);
        log.GetLogs()["Overflow"].Should().Be(0L);
        log.Store("Another", 0L);
        log.GetLogs().Should().NotContainKey("Another");
    }

    [Fact]
    public void Failed_Duration_Reservations_Should_Remain_Hidden_And_Not_Consume_Entry_Capacity()
    {
        var log = CreateStorageLog(new DiagnosticLogOptions { MaxNodes = 16, MaxScopeBytes = 4096 });
        var storedKeys = new[] { nameof(ScopedLog), "App_Name", "App_InstanceId", "App_NexusId",
            "App_NexusInstanceId", "App_Environment", "App_Environment_MachineName", ScopedLogConventions.KeyOfScopeCreatedAt };
        foreach (var key in storedKeys) log.Store(key, 0L);
        var counterKey = ScopedLogConventions.TimeSpentOnCounter("Work");
        var durationKey = ScopedLogConventions.TimeSpentOnKey("Work");
        using (var sizing = DiagnosticJsonSize.Rent())
        {
            var used = storedKeys.Sum(key => sizing.MeasureEntry(key, 0L));
            var counterReservation = sizing.Measure(counterKey) + 40;
            // Leave enough for the counter reservation, but not its paired duration reservation.
            var fillerBytes = 4096 - 2048 - used - counterReservation - 1;
            log.Store("Filler", new string('x', fillerBytes - sizing.Measure("Filler") - 4));
        }

        log.IncreaseTimeSpentOn("Work", TimeSpan.FromSeconds(1)).Should().Be(TimeSpan.Zero);
        log.GetLogs().Should().NotContainKey(counterKey).And.NotContainKey(durationKey);
        using var settings = SettingsProvider.Development();
        var copy = new ScopedLog(settings, new DiagnosticSanitizer(new DiagnosticLogOptions { MaxProcessingMilliseconds = 100 }));
        copy.CopyFrom(log);
        copy.GetLogs().Should().NotContainKey(counterKey).And.NotContainKey(durationKey);

        log.Store("Filler", "");
        for (var index = 0; index < 7; index++) log.Store($"Slot{index}", 0L);
        log.GetLogs()["Slot6"].Should().Be(0L);
        log.Store("Overflow", 0L);
        log.GetLogs().Should().NotContainKey("Overflow");

        log.Store("Filler", new string('x', 4096));
        log.Store(counterKey, 1L);
        log.GetLogs()[counterKey].Should().Be(1L);
        log.Increase("TimeSpentOn_Work_Counter").Should().Be(2L);
    }

    private static ScopedLog CreateStorageLog(DiagnosticLogOptions options)
    {
        using var settings = SettingsProvider.Development();
        var sanitizer = Substitute.For<IDiagnosticSanitizer>();
        sanitizer.Options.Returns(options);
        sanitizer.SanitizeKey(Arg.Any<string>()).Returns(call => call.Arg<string>());
        sanitizer.SanitizeValue(Arg.Any<string>(), Arg.Any<object>()).Returns(call => call[1]);
        sanitizer.GetStatus().Returns(new Dictionary<string, object>());
        return new ScopedLog(settings, sanitizer);
    }

    [Fact]
    public void Metrics_Should_Preserve_Concurrent_Updates_While_Snapshotting()
    {
        var log = CreateLog();
        Parallel.For(0, 1000, index =>
        {
            log.Increase("Items");
            log.IncreaseTimeSpentOn("Work", TimeSpan.FromSeconds(1));
            if (index % 100 == 0)
            {
                var snapshot = log.GetLogs();
                snapshot["Stats_Items"].Should().BeOfType<long>();
                snapshot["Stats_TimeSpentOn_Work_Seconds"].Should().BeOfType<double>();
            }
        });

        var result = log.GetLogs();
        result["Stats_Items"].Should().Be(1000L);
        result["Stats_TimeSpentOn_Work_Counter"].Should().Be(1000L);
        result["Stats_TimeSpentOn_Work_Seconds"].Should().Be(1000d);
    }

    [Fact]
    public void Repeated_Metrics_Measurements_And_Disabled_Flushes_Should_Not_Allocate_After_Warmup()
    {
        var log = CreateLog();
        var logger = NullLogger.Instance;
        var elapsed = TimeSpan.FromSeconds(1);
        for (var i = 0; i < 1000; i++)
        {
            log.Increase("Items");
            log.IncreaseTimeSpentOn("Work", elapsed);
            using var measurement = log.Measure(this, "MeasuredWork");
            logger.LogScoped(log);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            log.Increase("Items");
            log.IncreaseTimeSpentOn("Work", elapsed);
            using var measurement = log.Measure(this, "MeasuredWork");
            logger.LogScoped(log);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.Should().Be(0);
    }

    [Fact]
    public void Cached_Property_Metadata_Should_Honor_Attributes_And_Per_Call_Ignored_Names()
    {
        var log = CreateLog();
        log.AddProperties("First", new AttributedLogData(), nameof(AttributedLogData.Visible));
        log.AddProperties("Other", new AttributedLogData());

        var snapshot = log.GetLogs();
        snapshot["First.Visible"].Should().Be(ScopedLogConventions.IgnoredLogValue);
        snapshot["Other.Visible"].Should().Be("visible");
        snapshot["First.Secret"].Should().Be(ScopedLogConventions.IgnoredLogValue);
        snapshot["Other.Secret"].Should().Be(ScopedLogConventions.IgnoredLogValue);
    }

    private sealed class AttributedLogData
    {
        [SuppressMessage("Performance", "CA1822", Justification = "Exercises per-call exclusion of a cached instance property.")]
        public string Visible => "visible";
        [SuppressMessage("Performance", "CA1822", Justification = "Verifies that IgnoreLog skips the instance getter on every capture.")]
        [SuppressMessage("DRN.Classification", "DRN0015", Justification = "Unclassified ignored property verifies runtime getter suppression independently of classification.")]
        [IgnoreLog] public string Secret => throw new InvalidOperationException("Ignored getters must not execute.");
    }

    private static ScopedLog CreateLog() => new(SettingsProvider.Development());
}
