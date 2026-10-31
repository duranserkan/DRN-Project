using System.Text.Json;
using DRN.Framework.Utils.Logging.Diagnostics;

namespace DRN.Test.Unit.Tests.Framework.Utils.Logging;

public class DiagnosticLogFormatterTests
{
    [Fact]
    public void Structural_Redaction_Should_Precede_Fragmentation_While_Text_Is_Preserved()
    {
        var policy = new DiagnosticSanitizer(new DiagnosticLogOptions());
        var safe = policy.SanitizeText(new string('a', 4000) + " password=split-secret-probe; " + new string('z', 4000));
#pragma warning disable DRN0017 // Deliberately exercise runtime name-based redaction before fragmentation.
        var redacted = policy.SanitizeValue("Password", "field-secret-probe")!;
#pragma warning restore DRN0017
        var records = DiagnosticLogFormatter.Create(new Dictionary<string, object>
            { ["Message"] = safe, ["Password"] = redacted },
            new DiagnosticLogOptions { MaxRecordBytes = 4096 });
        records.Count.Should().BeGreaterThan(1);
        JsonSerializer.Serialize(records).Should().NotContain("field-secret-probe");
        using var reconstructed = JsonDocument.Parse(string.Concat(records.Skip(1).Select(part => (string)part["PayloadFragment"])));
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
        using var reconstructed = JsonDocument.Parse(string.Concat(parts.Select(part => (string)part["PayloadFragment"])));
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
}
