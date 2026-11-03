using static DRN.Test.Analyzer.Tests.DiagnosticClassificationAnalyzerTests;

namespace DRN.Test.Analyzer.Tests;

public sealed class ConfiguredCapturePathTests
{
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
