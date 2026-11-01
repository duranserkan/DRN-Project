using System.Text.Json;
using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Logging.Diagnostics;
using static DRN.Test.Unit.Tests.Framework.Utils.Logging.DiagnosticTestSupport;

// Deliberately capture nested unclassified dictionaries to verify runtime fallback.
#pragma warning disable DRN0020

namespace DRN.Test.Unit.Tests.Framework.Utils.Logging;

public sealed class DiagnosticDictionaryTests
{
    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public void Null_Classifications_Should_Fail_Closed(bool nullArray)
    {
        var sanitizer = Policy();
        var values = new Dictionary<string, object?> { ["Value"] = "must-not-escape" };
        DiagnosticKeyClassification[] classifications = nullArray ? null! : [null!];

        sanitizer.SanitizeDictionary("Fields", values, classifications).Should().Be(DiagnosticSanitizer.Invalid);
        sanitizer.GetStatus()["DiagnosticCaptureWarnings"].Should().BeEquivalentTo(new[] { "SanitizationFailed" });
    }

    [Fact]
    public void Deferred_Classified_Complex_Values_Should_Remain_Protected_Despite_Key_Exemptions()
    {
        using var settings = SettingsProvider.Development();
        var source = new ScopedLog(settings, Policy());
        var values = new Dictionary<string, object?> { ["Value"] = new ClassifiedPayload() };
        source.AddDictionary("Fields", values,
            new DiagnosticKeyClassification("Value", DiagnosticDataKind.NonSensitive, "Reviewed key name."));

        var copy = new ScopedLog(settings, Policy());
        copy.CopyFrom(source);
        foreach (var log in new[] { source, copy })
        {
            var raw = JsonSerializer.SerializeToElement(log.GetLogs(false)["Fields"]);
            raw.GetProperty("Value").GetProperty("Content").GetString().Should().Be("classified-complex-probe");
            var redacted = JsonSerializer.SerializeToElement(log.GetLogs()["Fields"]);
            redacted.GetProperty("Value").GetString().Should().Be(DiagnosticSanitizer.Redacted);
        }
    }

    [Sensitive] private sealed class ClassifiedPayload { public string Content => "classified-complex-probe"; }

    [Fact]
    public void Classifications_Should_Protect_Values_And_Exempt_Only_Exact_Scalar_Keys()
    {
        var values = new Dictionary<string, object?>
        {
            ["StatusCode"] = 200, ["Opaque"] = new ThrowingModel(), ["Contact"] = "private",
            ["Password"] = "private", ["Count"] = 3,
            ["Nested"] = new Dictionary<string, object?> { ["StatusCode"] = 201 },
            ["statuscode"] = 202
        };
        IDiagnosticSanitizer sanitizer = Policy();
        var data = JsonSerializer.SerializeToElement(sanitizer.SanitizeDictionary("ClientReport", values,
            new("StatusCode", DiagnosticDataKind.NonSensitive, "HTTP status."),
            new("Opaque", DiagnosticDataKind.Sensitive), new("Contact", DiagnosticDataKind.Personal)));
        data.GetProperty("StatusCode").GetInt32().Should().Be(200);
        data.GetProperty("Count").GetInt32().Should().Be(3);
        foreach (var key in new[] { "Opaque", "Contact", "Password", "statuscode" })
            data.GetProperty(key).GetString().Should().Be(DiagnosticSanitizer.Redacted);
        data.GetProperty("Nested").GetProperty("StatusCode").GetString().Should().Be(DiagnosticSanitizer.Redacted);
    }

    [Fact]
    public void Invalid_Exemptions_Should_Fail_Closed_And_Positive_Classifications_Should_Win()
    {
        var values = new Dictionary<string, object?> { ["StatusCode"] = 200 };
        foreach (var rule in new[]
                 {
                     new DiagnosticKeyClassification("StatusCode", DiagnosticDataKind.NonSensitive, " "),
                     new DiagnosticKeyClassification("StatusCode", (DiagnosticDataKind)99),
                     new DiagnosticKeyClassification("", DiagnosticDataKind.Sensitive)
                 })
            Policy().SanitizeDictionary("Response", values, rule).Should().Be("[diagnostic field unavailable]");
        var exception = new DiagnosticKeyClassification("StatusCode", DiagnosticDataKind.NonSensitive, "HTTP status.");
        foreach (var rules in new[]
                 {
                     new[] { exception, new DiagnosticKeyClassification("StatusCode", DiagnosticDataKind.Personal) },
                     new[] { new DiagnosticKeyClassification("StatusCode", DiagnosticDataKind.Sensitive), exception }
                 })
            JsonSerializer.SerializeToElement(Policy().SanitizeDictionary("Response", values, rules))
                .GetProperty("StatusCode").GetString().Should().Be(DiagnosticSanitizer.Redacted);
        values["StatusCode"] = new ThrowingModel();
        JsonSerializer.SerializeToElement(Policy().SanitizeDictionary("Response", values, exception))
            .GetProperty("StatusCode").GetString().Should().Be("[diagnostic field unavailable]");
        values["StatusCode"] = new ProtectedModel();
        JsonSerializer.SerializeToElement(Policy().SanitizeDictionary("Response", values, exception))
            .GetProperty("StatusCode").GetString().Should().Be(DiagnosticSanitizer.Redacted);
    }

    [Fact]
    public void Native_Copy_Should_Preserve_Reviewed_Values_And_Respect_Stricter_Destinations()
    {
        using var settings = SettingsProvider.Development();
        IScopedLog source = new ScopedLog(settings, Policy());
        var values = new Dictionary<string, object?> { ["StatusCode"] = 200 };
        source.AddDictionary("Response", values, new DiagnosticKeyClassification("StatusCode", DiagnosticDataKind.NonSensitive, "HTTP status."));
        values["StatusCode"] = "mutated";
        var copy = new ScopedLog(settings, Policy());
        copy.CopyFrom(source);
        JsonSerializer.SerializeToElement(copy.GetLogs()["Response"]).GetProperty("StatusCode").GetInt32().Should().Be(200);
        var stricter = new ScopedLog(settings, Policy("StatusCode"));
        stricter.CopyFrom(source);
        stricter.GetLogs()["Response"].Should().Be(DiagnosticSanitizer.Redacted);
    }

    [Fact]
    public void Configured_Rules_And_Capture_Budgets_Should_Remain_Authoritative()
    {
        var values = new Dictionary<string, object?> { ["StatusCode"] = 200, ["Count"] = 3 };
        var rule = new DiagnosticKeyClassification("StatusCode", DiagnosticDataKind.NonSensitive, "HTTP status.");
        JsonSerializer.SerializeToElement(Policy("Response.StatusCode").SanitizeDictionary("Response", values, rule))
            .GetProperty("StatusCode").GetString().Should().Be(DiagnosticSanitizer.Redacted);
        var bounded = new DiagnosticSanitizer(new DiagnosticLogOptions { MaxCollectionEntries = 1, MaxProcessingMilliseconds = 100 });
        JsonSerializer.SerializeToElement(bounded.SanitizeDictionary("Response", values, rule))
            .GetProperty("_diagnostic_limit").GetString().Should().Be(DiagnosticSanitizer.Limited);
    }

    private sealed class ThrowingModel { public string Value => throw new InvalidOperationException("Must not read."); }

    [Fact]
    public void Foreign_Implementations_Should_Not_Receive_Unprotected_Dictionary_Values()
    {
        var values = new Dictionary<string, object?> { ["Opaque"] = new ThrowingModel() };
        var rule = new DiagnosticKeyClassification("Opaque", DiagnosticDataKind.Sensitive);
        var log = Substitute.For<IScopedLog>();
        log.AddDictionary("Response", values, rule);
        log.Received().Add("Response", "[diagnostic field unavailable]");
        var sanitizer = Substitute.For<IDiagnosticSanitizer>();
        sanitizer.SanitizeDictionary("Response", values, rule).Should().Be("[diagnostic field unavailable]");
        sanitizer.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public void Scalar_Exemptions_Should_Still_Sanitize_Urls_And_Nonfinite_Numbers()
    {
        var values = new Dictionary<string, object?>
        {
            ["Target"] = new Uri("https://example.test/?token=private"), ["Count"] = double.NaN
        };
        var data = JsonSerializer.SerializeToElement(Policy().SanitizeDictionary("Response", values,
            new("Target", DiagnosticDataKind.NonSensitive, "Destination URL."),
            new("Count", DiagnosticDataKind.NonSensitive, "Measurement.")));
        data.GetProperty("Target").GetString().Should().NotContain("private");
        data.GetProperty("Count").GetString().Should().Be("[diagnostic field unavailable]");
    }

    [Sensitive] private sealed class ProtectedModel { public string Value => throw new InvalidOperationException("Must not read."); }
}
