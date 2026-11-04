using static DRN.Test.Analyzer.Tests.DiagnosticClassificationAnalyzerTests;

namespace DRN.Test.Analyzer.Tests;

public sealed class ConfiguredCapturePathTests
{
    [Theory]
    [InlineData(3, "DRN0017")]
    [InlineData(14, "DRN0025")]
    public async Task Configured_Paths_After_Shared_Subtrees_Should_Report_Matches_Or_Incomplete_Analysis(
        int levels, string expectedDiagnostic)
    {
        // Revisited shared types exhaust work without hitting the depth or cycle limits.
        var branches = string.Join("\n", Enumerable.Range(0, levels).Select(level =>
            $"public class Branch{level} {{ public Branch{level + 1} A {{ get; set; }} public Branch{level + 1} B {{ get; set; }} }}"));
        var compilation = Compilation($$"""
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog { void Add(string key, object value); }
            }
            {{branches}}
            public class Branch{{levels}} { public int Value { get; set; } }
            public class Model
            {
                public Branch0 Tree { get; set; }
                public string Detail { get; set; }
            }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log, Model model)
                {
                    log.Add("Payload", model);
                }
            }
            """);
        compilation.GetDiagnostics().Should().NotContain(item => item.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        var diagnostics = await Analyze(compilation, new ProjectDiagnosticRulesTests.RuleFile(
            "data-classification-rules.json", """{"sensitiveFieldPaths":["Payload.Detail"]}"""));

        var diagnostic = diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be(expectedDiagnostic);
        diagnostic.Severity.Should().Be(Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan)
            .Should().Be("log.Add(\"Payload\", model)");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("\"[redacted]\"")]
    public async Task Configured_Dictionary_Paths_Should_Check_User_Defined_Conversions(string literal)
    {
        var diagnostics = await Analyze(Compilation($$"""
            using System.Collections.Generic;
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog
                {
                    void AddDictionary<T>(string key, IReadOnlyDictionary<string, T> values);
                }
            }
            public sealed class ConvertedValue
            {
                public static implicit operator ConvertedValue(string value) => new();
                public static explicit operator string(ConvertedValue value) => "private";
            }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log)
                {
                    log.AddDictionary("Payload", new Dictionary<string, object> { ["Detail"] = (string)(ConvertedValue){{literal}} });
                    log.AddDictionary("Payload", new Dictionary<string, object> { ["Detail"] = (object)(string){{literal}} });
                }
            }
            """), new ProjectDiagnosticRulesTests.RuleFile("data-classification-rules.json",
            """{"sensitiveFieldPaths":["Payload.Detail"]}"""));

        diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRN0020");
        diagnostics.Single().Location.SourceTree!.GetText().ToString(diagnostics.Single().Location.SourceSpan)
            .Should().Be("log.AddDictionary(\"Payload\", new Dictionary<string, object> { [\"Detail\"] = (string)(ConvertedValue)" + literal + " })");
    }

    [Theory]
    [InlineData("native.AddDictionary(")]
    [InlineData("nativeSanitizer.SanitizeDictionary(")]
    [InlineData("log.AddDictionary(")]
    [InlineData("sanitizer.SanitizeDictionary(")]
    [InlineData("DiagnosticCaptureExtensions.AddDictionary(log, ")]
    public async Task Dictionary_APIs_Should_Require_Protection_For_Configured_Paths(string capture)
    {
        var diagnostics = await Analyze(Compilation($$"""
            using System.Collections.Generic;
            using DRN.Framework.Utils.Logging;
            using DRN.Framework.Utils.Logging.Diagnostics;
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog { }
                public class ScopedLog
                {
                    public void AddDictionary<T>(string key, IReadOnlyDictionary<string, T> values,
                        params DiagnosticKeyClassification[] classifications) { }
                }
            }
            namespace DRN.Framework.Utils.Logging.Diagnostics
            {
                public enum DiagnosticDataKind { Sensitive = 1, Personal = 2, NonSensitive = 3 }
                public class DiagnosticKeyClassification
                {
                    public DiagnosticKeyClassification(string key, DiagnosticDataKind kind, string reason = null) { }
                }
                public interface IDiagnosticSanitizer { }
                public class DiagnosticSanitizer
                {
                    public object SanitizeDictionary<T>(string path, IReadOnlyDictionary<string, T> values,
                        params DiagnosticKeyClassification[] classifications) => null;
                }
                public static class DiagnosticCaptureExtensions
                {
                    public static void AddDictionary<T>(this IScopedLog log, string key, IReadOnlyDictionary<string, T> values,
                        params DiagnosticKeyClassification[] classifications) { }
                    public static object SanitizeDictionary<T>(this IDiagnosticSanitizer sanitizer, string path,
                        IReadOnlyDictionary<string, T> values, params DiagnosticKeyClassification[] classifications) => null;
                }
            }
            public class Caller
            {
                public void Run(ScopedLog native, DiagnosticSanitizer nativeSanitizer, IScopedLog log,
                    IDiagnosticSanitizer sanitizer, string value, DiagnosticKeyClassification[] unknown)
                {
                    var values = new Dictionary<string, object> { ["Detail"] = value };
                    // DiagnosticNameExemption: Payload.Detail | A comment cannot waive this configured path.
                    {{capture}}"Payload", values);
                    {{capture}}"Payload", values, new DiagnosticKeyClassification("Detail", DiagnosticDataKind.NonSensitive, "Reviewed"));
                    {{capture}}"Payload", values, new DiagnosticKeyClassification("Other", DiagnosticDataKind.Sensitive));
                    {{capture}}"Payload", values, new DiagnosticKeyClassification("detail", DiagnosticDataKind.Sensitive));
                    {{capture}}"Payload", values, unknown);
                    {{capture}}"Payload", values, new DiagnosticKeyClassification("Detail", DiagnosticDataKind.Sensitive));
                    {{capture}}"Payload", values, new DiagnosticKeyClassification("Detail", DiagnosticDataKind.Personal));
                    {{capture}}"Payload", values, new DiagnosticKeyClassification("Detail", DiagnosticDataKind.Sensitive),
                        new DiagnosticKeyClassification("Detail", DiagnosticDataKind.NonSensitive, "Reviewed"));
                    {{capture}}"Payload", new Dictionary<string, object> { ["Detail"] = "[redacted]" });
                    {{capture}}"Payload", new Dictionary<string, object> { ["Detail"] = "" });
                    {{capture}}"Payload", new Dictionary<string, object> { ["Detail"] = null });
                    {{capture}}"Payload", new Dictionary<string, object>
                    {
                        ["Detail"] = new Dictionary<string, object> { ["Password"] = value }
                    }, new DiagnosticKeyClassification("Detail", DiagnosticDataKind.Sensitive));
                    {{capture}}"Other", values);
                }
            }
            """), new ProjectDiagnosticRulesTests.RuleFile("data-classification-rules.json",
            """{"sensitiveFieldPaths":["Payload.Detail"]}"""));

        diagnostics.Should().HaveCount(6);
        diagnostics.Should().ContainSingle(item => item.Id == "DRN0024");
        diagnostics.Where(item => item.Id == "DRN0020")
            .Select(item => item.Location.SourceTree!.GetText().ToString(item.Location.SourceSpan))
            .Should().BeEquivalentTo(
                capture + "\"Payload\", values)",
                capture + "\"Payload\", values, new DiagnosticKeyClassification(\"Detail\", DiagnosticDataKind.NonSensitive, \"Reviewed\"))",
                capture + "\"Payload\", values, new DiagnosticKeyClassification(\"Other\", DiagnosticDataKind.Sensitive))",
                capture + "\"Payload\", values, new DiagnosticKeyClassification(\"detail\", DiagnosticDataKind.Sensitive))",
                capture + "\"Payload\", values, unknown)");
    }

    [Fact]
    public async Task Scope_Dictionaries_Should_Preserve_Nested_Paths_Through_Comments_And_Local_Writes()
    {
        var diagnostics = await Analyze(Compilation("""
            using Microsoft.Extensions.Logging;
            using System.Collections.Generic;
            public class Caller
            {
                public void Run(ILogger log, string value)
                {
                    // DiagnosticNameExemption: TokenCount | Parent name only.
                    log.BeginScope(new Dictionary<string, object>
                    {
                        ["TokenCount"] = new Dictionary<string, object> { ["Detail"] = value }
                    });
                    var child = new Dictionary<string, object>();
                    child["Detail"] = value;
                    var state = new Dictionary<string, object>();
                    state["Payload"] = child;
                    // DiagnosticNameExemption: Payload.Detail | Configured paths cannot be waived.
                    log.BeginScope(state);
                    log.BeginScope(new Dictionary<string, object> { ["Other"] = child });
                }
            }
            """), new ProjectDiagnosticRulesTests.RuleFile("data-classification-rules.json",
            """{"sensitiveFieldPaths":["TokenCount.Detail","Payload.Detail"]}"""));

        diagnostics.Should().HaveCount(3);
        diagnostics.Count(item => item.Id == "DRN0022").Should().Be(2);
        diagnostics.Should().ContainSingle(item => item.Id == "DRN0024");
        diagnostics.Should().ContainSingle(item => item.GetMessage().Contains("'TokenCount.Detail'", StringComparison.Ordinal));
        diagnostics.Should().ContainSingle(item => item.GetMessage().Contains("'Payload.Detail'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Reasons_And_Policies_Should_Acknowledge_Names_But_Enforce_Configured_Paths()
    {
        var diagnostics = await Analyze(Compilation("""
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog
                {
                    void Add(string key, object value, string exemptionReason);
                    void AddWithPolicy(string key, object value, object policy);
                    void AddPropertiesWithPolicy<T>(string prefix, T value, object policy) where T : class;
                }
            }
            namespace DRN.Framework.Utils.Logging.Diagnostics
            {
                public interface IDiagnosticSanitizer
                {
                    object SanitizeValue(string path, object value, string exemptionReason);
                    object SanitizeValue(string path, object value, object policy);
                }
            }
            public class Model { public string Detail { get; set; } }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log,
                    DRN.Framework.Utils.Logging.Diagnostics.IDiagnosticSanitizer sanitizer, Model model, object policy)
                {
                    log.Add("Payload", "value", "Reviewed name.");
                    log.AddWithPolicy("Payload", "value", policy);
                    log.AddPropertiesWithPolicy("Report", model, policy);
                    sanitizer.SanitizeValue("Payload", "value", "Reviewed name.");
                    sanitizer.SanitizeValue("Report", model, policy);
                    log.Add("TokenCount", 1, "Reviewed metric.");
                    log.AddWithPolicy("Password", "reviewed", policy);
                    log.AddPropertiesWithPolicy("Other", model, policy);
                }
            }
            """), new ProjectDiagnosticRulesTests.RuleFile("data-classification-rules.json",
            """{"sensitiveFieldPaths":["Payload","Report.Detail"]}"""));

        diagnostics.Should().HaveCount(5).And.OnlyContain(item => item.Id == "DRN0017");
    }

    [Fact]
    public async Task Extension_Policies_Should_Check_The_Value_And_Path_After_The_Receiver()
    {
        var diagnostics = await Analyze(Compilation("""
            using DRN.Framework.Utils.Logging.Diagnostics;
            namespace DRN.Framework.Utils.Logging { public interface IScopedLog { } }
            namespace DRN.Framework.Utils.Logging.Diagnostics
            {
                public interface IDiagnosticSanitizer { }
                public static class DiagnosticCaptureExtensions
                {
                    public static void Add(this DRN.Framework.Utils.Logging.IScopedLog log, string key, object value, string exemptionReason) { }
                    public static void AddWithPolicy(this DRN.Framework.Utils.Logging.IScopedLog log, string key, object value, object policy) { }
                    public static void AddPropertiesWithPolicy<T>(this DRN.Framework.Utils.Logging.IScopedLog log, string prefix, T value, object policy) where T : class { }
                    public static object SanitizeWithPolicy(this IDiagnosticSanitizer sanitizer, string path, object value, object policy) => value;
                }
            }
            public class Model { public string Detail { get; set; } }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log, IDiagnosticSanitizer sanitizer, Model model, object policy)
                {
                    log.Add("Payload", "value", "Reviewed name.");
                    log.AddWithPolicy("Payload", "value", policy);
                    DiagnosticCaptureExtensions.AddWithPolicy(log, "Payload", "value", policy);
                    log.AddPropertiesWithPolicy("Report", model, policy);
                    sanitizer.SanitizeWithPolicy("Report", model, policy);
                    log.AddWithPolicy("Password", "reviewed", policy);
                }
            }
            """), new ProjectDiagnosticRulesTests.RuleFile("data-classification-rules.json",
            """{"sensitiveFieldPaths":["Payload","Report.Detail"]}"""));

        diagnostics.Should().HaveCount(5).And.OnlyContain(item => item.Id == "DRN0017");
    }
}
