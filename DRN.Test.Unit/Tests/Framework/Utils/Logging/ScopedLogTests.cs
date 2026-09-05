using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.Utils.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DRN.Test.Unit.Tests.Framework.Utils.Logging;

public class ScopedLogTests
{
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
            source.Add("Key.\u00e9", "composed");
            source.Add("Key.z", "lowercase");
            source.Add("Key.e\u0301", "decomposed");
            source.Add("Key.Z", "uppercase");

            var destination = CreateLog();
            destination.CopyFrom(source);

            foreach (var snapshot in new[] { source.GetLogs(), destination.GetLogs() })
            {
                snapshot.Keys.Where(key => key.StartsWith("Key.", StringComparison.Ordinal))
                    .Should().Equal("Key.Z", "Key.e\u0301", "Key.z", "Key.\u00e9");
                snapshot["Key.\u00e9"].Should().Be("composed");
                snapshot["Key.e\u0301"].Should().Be("decomposed");
                snapshot["Key.Z"].Should().Be("uppercase");
                snapshot["Key.z"].Should().Be("lowercase");
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

        log.AddProperties("Map", new Dictionary<string, string> { ["key"] = "value" });

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
        public string Visible => "visible";
        public string Hidden { private get; set; } = "hidden";
        public static string Static => "static";
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
        destination.Add("DestinationKey", "destination");
        destination.AddWarning("destination-warning");
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
        log.IncreaseTimeSpentOn("Work", TimeSpan.FromSeconds(2), "Custom_").Should().Be(TimeSpan.FromSeconds(12));
        log.Add("Custom_TimeSpentOn_Work_Seconds", "reset");
        log.IncreaseTimeSpentOn("Work", TimeSpan.FromSeconds(3), "Custom_").Should().Be(TimeSpan.FromSeconds(3));
        log.Add("Custom_TimeSpentOn_Work_Seconds", 1d);
        log.IncreaseTimeSpentOn("Work", TimeSpan.FromSeconds(2), "Custom_").Should().Be(TimeSpan.FromSeconds(3));
        log.GetLogs()["Custom_TimeSpentOn_Work_Counter"].Should().Be(3L);
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
        log.AddProperties("Second", new AttributedLogData());

        var snapshot = log.GetLogs();
        snapshot["First.Visible"].Should().Be(ScopedLogConventions.IgnoredLogValue);
        snapshot["Second.Visible"].Should().Be("visible");
        snapshot["First.Secret"].Should().Be(ScopedLogConventions.IgnoredLogValue);
        snapshot["Second.Secret"].Should().Be(ScopedLogConventions.IgnoredLogValue);
    }

    private sealed class AttributedLogData
    {
        public string Visible => "visible";
        [IgnoreLog] public string Secret => throw new InvalidOperationException("Ignored getters must not execute.");
    }

    private static ScopedLog CreateLog() => new(AppSettings.Development());
}
