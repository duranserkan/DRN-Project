using System.Text.Json;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Logging.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DRN.Test.Unit.Tests.Framework.Utils.Logging;

public class DiagnosticLogFormatterTests
{
    [Fact]
    public void Events_Should_Be_Complete_On_The_Parent_Without_Consuming_Diagnostic_Budgets()
    {
        var text = new string('e', 20000);
        var primary = new ScopeEvent(new EventId(42, text), text, text);
        var additional = new ScopeEvent(new EventId(43, text), text, text);
        var detail = new string('d', 9000);
        var records = DiagnosticLogFormatter.Create(new Dictionary<string, object> { ["Detail"] = detail },
            new DiagnosticLogOptions { MaxRecordBytes = 4096 }, new ScopeEventSnapshot(primary, [additional], 2));

        records.Count.Should().BeGreaterThan(1);
        records[0]["EventId"].Should().Be(42);
        records[0]["OmittedEventCount"].Should().Be(2L);
        records[0]["EventName"].Should().Be(text);
        records[0]["EventOutcome"].Should().Be(text);
        records[0]["EventReason"].Should().Be(text);
        ((List<object>)records[0]["AdditionalEvents"]).Should().Equal(additional);
        JsonSerializer.SerializeToUtf8Bytes(records[0]).Length.Should().BeGreaterThan(4096);
        foreach (var child in records.Skip(1))
        {
            child.Should().NotContainKey("AdditionalEvents");
            child.Should().NotContainKey("OmittedEventCount");
            JsonSerializer.SerializeToUtf8Bytes(child).Length.Should().BeLessThanOrEqualTo(4096);
        }
        using var payload = Reconstruct(records.Skip(1));
        payload.RootElement.GetProperty("Detail").GetString().Should().Be(detail);
    }

    [Fact]
    public void Escaped_Fragments_Should_Stay_Within_Byte_Limits_And_Reconstruct_Exactly()
    {
        var text = string.Concat(Enumerable.Repeat("\"\\\n\t<>& Türkçe 😀界", 400));
        var records = DiagnosticLogFormatter.Create(new Dictionary<string, object> { ["Detail"] = text },
            new DiagnosticLogOptions { MaxRecordBytes = 4096, MaxRecords = 16 });
        records.Count.Should().BeGreaterThan(1);
        foreach (var record in records)
            JsonSerializer.SerializeToUtf8Bytes(record).Length.Should().BeLessThanOrEqualTo(4096);
        using var payload = Reconstruct(records.Skip(1));
        payload.RootElement.GetProperty("Detail").GetString().Should().Be(text);
    }

    [Fact]
    public void Structural_Redaction_Should_Precede_Fragmentation_While_Text_Is_Preserved()
    {
        var policy = new DiagnosticSanitizer(new DiagnosticLogOptions(), new FrozenTimeProvider());
        var safe = policy.SanitizeText(new string('a', 4000) + " password=split-secret-probe; " + new string('z', 4000));
        var classified = policy.SanitizeDictionary("Fields", new Dictionary<string, string> { ["Password"] = "field-secret-probe" },
            new DiagnosticKeyClassification("Password", DiagnosticDataKind.Sensitive));
        var redacted = JsonSerializer.SerializeToElement(classified).GetProperty("Password").GetString()!;
        var records = DiagnosticLogFormatter.Create(new Dictionary<string, object>
            { ["Message"] = safe, ["Password"] = redacted },
            new DiagnosticLogOptions { MaxRecordBytes = 4096 });
        records.Count.Should().BeGreaterThan(1);
        JsonSerializer.Serialize(records).Should().NotContain("field-secret-probe");
        using var reconstructed = Reconstruct(records.Skip(1));
        reconstructed.RootElement.GetProperty("Message").GetString().Should().Be(safe);
        reconstructed.RootElement.GetProperty("Message").GetString().Should().Contain("split-secret-probe");
        reconstructed.RootElement.GetProperty("Password").GetString().Should().Be(DiagnosticSanitizer.Redacted);
    }

    [Fact]
    public void Small_Diagnostics_Should_Stay_Grouped_With_One_Manifest()
    {
        var records = DiagnosticLogFormatter.Create(new Dictionary<string, object> { ["Message"] = "Failure" }, new DiagnosticLogOptions());
        records.Should().ContainSingle();
        records[0]["PartCount"].Should().Be(1);
        records[0]["LogRecordId"].Should().Be(records[0]["ParentLogRecordId"]);
        records[0].Should().NotContainKey("PayloadFragment");
    }

    [Fact]
    public void Overflow_Should_Reconstruct_Exactly_And_Expose_Missing_Records()
    {
        var detail = new string('a', 9000) + "\n\" Unicode: 😀 tail";
        var options = new DiagnosticLogOptions { MaxRecordBytes = 4096 };
        var records = DiagnosticLogFormatter.Create(new Dictionary<string, object>
        {
            ["CorrelationId"] = "scope", ["TraceId"] = "trace", ["TraceIdentifier"] = "request",
            ["ClientError.ClientErrorId"] = new string('b', 32), ["Detail"] = detail, ["EventId"] = 42
        }, options);
        records.Count.Should().BeInRange(2, 8);
        foreach (var record in records)
        {
            JsonSerializer.SerializeToUtf8Bytes(record).Length.Should().BeLessThanOrEqualTo(options.MaxRecordBytes);
            record["PartCount"].Should().Be(records.Count);
            record["CorrelationId"].Should().Be("scope");
            record["TraceId"].Should().Be("trace");
            record["TraceIdentifier"].Should().Be("request");
            record["ClientError.ClientErrorId"].Should().Be(new string('b', 32));
            ((string[])record["RelatedLogRecordIds"]).Should().Equal(records.Select(item => (string)item["LogRecordId"]));
        }
        var parts = records.Skip(1).OrderBy(record => (int)record["PayloadFragmentIndex"]).ToArray();
        parts.Should().OnlyContain(part => (int)part["PayloadFragmentCount"] == parts.Length);
        using var reconstructed = Reconstruct(parts);
        reconstructed.RootElement.GetProperty("Detail").GetString().Should().Be(detail);
        var delivered = records.Skip(1).Select(record => (string)record["LogRecordId"]);
        ((string[])records[1]["RelatedLogRecordIds"]).Except(delivered).Should().Equal((string)records[0]["LogRecordId"]);
    }

    [Fact]
    public void Total_Limit_Should_Produce_Valid_Json_And_Explicit_Loss()
    {
        var options = new DiagnosticLogOptions { MaxRecordBytes = 4096, MaxRecords = 1 };
        var records = DiagnosticLogFormatter.Create(new Dictionary<string, object>
            { ["CorrelationId"] = "scope", ["Detail"] = new string('x', 20000) }, options);
        records.Should().ContainSingle();
        records[0]["DiagnosticCaptureLimited"].Should().Be(true);
        records[0]["CorrelationId"].Should().Be("scope");
        JsonSerializer.SerializeToUtf8Bytes(records[0]).Length.Should().BeLessThanOrEqualTo(options.MaxRecordBytes);
    }

    private static JsonDocument Reconstruct(IEnumerable<Dictionary<string, object>> fragments) =>
        JsonDocument.Parse(string.Concat(fragments.Select(fragment => (string)fragment["PayloadFragment"])));

    private sealed class FrozenTimeProvider : TimeProvider
    {
        public override long GetTimestamp() => 0;
    }
}
