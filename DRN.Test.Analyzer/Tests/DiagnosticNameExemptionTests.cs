using static DRN.Test.Analyzer.Tests.DiagnosticClassificationAnalyzerTests;

namespace DRN.Test.Analyzer.Tests;

public sealed class DiagnosticNameExemptionTests
{
    [Theory]
    [InlineData("DRN.Framework.Utils.Logging", "IScopedLog", "Add")]
    [InlineData("DRN.Framework.Utils.Logging", "IScopedLog", "AddToList")]
    [InlineData("DRN.Framework.Utils.Logging.Diagnostics", "IDiagnosticSanitizer", "SanitizeValue")]
    public async Task Dictionary_Comments_Should_Match_Exact_Keys_Only_On_The_Adjacent_Capture(
        string ownerNamespace, string ownerType, string method)
    {
        var diagnostics = await Analyze(Compilation($$"""
            using System.Collections.Generic;
            namespace {{ownerNamespace}}
            {
                public interface {{ownerType}} { void {{method}}(string key, object value); }
            }
            public class Caller
            {
                public void Run({{ownerNamespace}}.{{ownerType}} capture)
                {
                    var data = new Dictionary<string, object>();
                    data["TokenCount"] = 3;
                    // DiagnosticNameExemption: TokenCount | Reviewed metric fixture.
                    capture.{{method}}("Allowed", data);
                    capture.{{method}}("Next", data);
                    // DiagnosticNameExemption: tokencount | Case does not match.
                    capture.{{method}}("Case", data);
                    // DiagnosticNameExemption: TokenCount | Detached comment.

                    capture.{{method}}("Detached", data);
                    // DiagnosticNameExemption: TokenCount | Does not cross a lambda boundary.
                    System.Action callback = () => capture.{{method}}("Callback", data);
                }
            }
            """));

        diagnostics.Should().HaveCount(7);
        diagnostics.Count(item => item.Id == "DRN0024").Should().Be(3);
        diagnostics.Where(item => item.Id == "DRN0020").Select(item => item.Location.SourceTree!.GetText().ToString(item.Location.SourceSpan))
            .Should().BeEquivalentTo($"capture.{method}(\"Next\", data)", $"capture.{method}(\"Case\", data)",
                $"capture.{method}(\"Detached\", data)", $"capture.{method}(\"Callback\", data)");
    }

    [Fact]
    public async Task Dictionary_Comments_Should_Keep_Children_And_Configured_Paths_Checked()
    {
        var diagnostics = await Analyze(Compilation("""
            using System.Collections.Generic;
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog { void Add(string key, object value); }
            }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log, string prefix)
                {
                    var data = new Dictionary<string, object>
                    {
                        ["TokenCount"] = new Dictionary<string, object>
                        {
                            ["Password"] = "private",
                            ["Detail"] = "payload"
                        }
                    };
                    // DiagnosticNameExemption: TokenCount | Parent only.
                    log.Add("Other", data);
                    // DiagnosticNameExemption: TokenCount | Parent name.
                    // DiagnosticNameExemption: Password | Child name.
                    // DiagnosticNameExemption: Detail | Cannot waive a configured path.
                    // DiagnosticNameExemption: Data.TokenCount.Detail | Full path cannot waive it either.
                    log.Add("Data", data);
                    // DiagnosticNameExemption: Protected | Known key still matches a configured path with a dynamic prefix.
                    log.Add(prefix, new Dictionary<string, object> { ["Protected"] = "payload" });
                }
            }
            """), new ProjectDiagnosticRulesTests.RuleFile("data-classification-rules.json",
            """{"sensitiveFieldPaths":["Data.TokenCount.Detail","Protected"]}"""));

        diagnostics.Should().HaveCount(6);
        diagnostics.Count(item => item.Id == "DRN0024").Should().Be(3);
        diagnostics.Should().ContainSingle(item => item.GetMessage().Contains("'Password'", StringComparison.Ordinal));
        diagnostics.Should().ContainSingle(item => item.GetMessage().Contains("'Data.TokenCount.Detail'", StringComparison.Ordinal));
        diagnostics.Should().ContainSingle(item => item.GetMessage().Contains("'Protected'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Malformed_Dictionary_Comments_Should_Not_Suppress_Capture_Diagnostics()
    {
        var diagnostics = await Analyze(Compilation("""
            using System.Collections.Generic;
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog { void Add(string key, object value); }
            }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log)
                {
                    // DiagnosticNameExemption: TokenCount |
                    log.Add("Data", new Dictionary<string, object> { ["TokenCount"] = 3 });
                }
            }
            """));

        diagnostics.Should().HaveCount(2);
        diagnostics.Should().ContainSingle(item => item.Id == "DRN0020");
        diagnostics.Should().ContainSingle(item => item.Id == "DRN0023");
    }

    [Theory]
    [InlineData("Add")]
    [InlineData("AddToList")]
    [InlineData("AddIfNotNullOrEmpty")]
    public async Task Scoped_Name_Comments_Should_Exempt_Only_The_Exact_Adjacent_Capture(string method)
    {
        var diagnostics = await Analyze(Compilation($$"""
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog { void {{method}}(string key, string value); }
            }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log, string value)
                {
                    // DiagnosticNameExemption: Request_Token | Deliberate unclassified fixture.
                    log.{{method}}("Request_Token", value);
                    log.{{method}}("Request_Token", value);
                    // DiagnosticNameExemption: request_token | Case does not match.
                    log.{{method}}("Request_Token", value);
                    // DiagnosticNameExemption: Request_Token | Detached comment.

                    log.{{method}}("Request_Token", value);
                    // DiagnosticNameExemption: Request_Token | Different names remain checked.
                    log.{{method}}("Password", value);
                }
            }
            """));

        diagnostics.Should().HaveCount(7);
        diagnostics.Count(item => item.Id == "DRN0017").Should().Be(4);
        diagnostics.Count(item => item.Id == "DRN0024").Should().Be(3);
    }

    [Fact]
    public async Task Sanitizer_Comments_Should_Keep_Malformed_Comments_And_Configured_Paths_Checked()
    {
        var diagnostics = await Analyze(Compilation("""
            namespace DRN.Framework.Utils.Logging.Diagnostics
            {
                public interface IDiagnosticSanitizer { object SanitizeValue(string path, object value); }
            }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.Diagnostics.IDiagnosticSanitizer sanitizer, string value)
                {
                    // DiagnosticNameExemption: Password | Deliberate unclassified fixture.
                    sanitizer.SanitizeValue("Password", value);
                    // DiagnosticNameExemption: Password |
                    sanitizer.SanitizeValue("Password", value);
                    // DiagnosticNameExemption: Payload | Configured paths remain enforced.
                    sanitizer.SanitizeValue("Payload", value);
                }
            }
            """), new ProjectDiagnosticRulesTests.RuleFile("data-classification-rules.json",
            """{"sensitiveFieldPaths":["Payload"]}"""));

        diagnostics.Should().HaveCount(4);
        diagnostics.Should().ContainSingle(item => item.Id == "DRN0024");
        diagnostics.Count(item => item.Id == "DRN0017").Should().Be(2);
        diagnostics.Should().ContainSingle(item => item.Id == "DRN0023");
    }

    [Fact]
    public async Task Scoped_Comments_Should_Preserve_Configured_Paths_Classified_Values_And_Declarations()
    {
        var diagnostics = await Analyze(Compilation("""
            using DRN.Framework.SharedKernel.Attributes;
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog { void Add(string key, object value); }
            }
            public class Input
            {
                [Sensitive] public string Value => "private";
                public string Password => "private";
            }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log, Input input, string value)
                {
                    // DiagnosticNameExemption: Payload | Cannot waive configured paths.
                    log.Add("Payload", value);
                    // DiagnosticNameExemption: Password | Cannot waive a classified value.
                    log.Add("Password", input.Value);
                    // DiagnosticNameExemption: Model.Password | Cannot waive declaration requirements.
                    log.Add("Model", input);
                }
            }
            """), new ProjectDiagnosticRulesTests.RuleFile("data-classification-rules.json",
            """{"sensitiveFieldPaths":["Payload"]}"""));

        diagnostics.Should().HaveCount(5);
        diagnostics.Count(item => item.Id == "DRN0024").Should().Be(2);
        diagnostics.Should().ContainSingle(item => item.Id == "DRN0015");
        diagnostics.Should().ContainSingle(item => item.Id == "DRN0017");
        diagnostics.Should().ContainSingle(item => item.Id == "DRN0022");
    }

    [Fact]
    public async Task External_Property_Exemptions_Should_Still_Inspect_Children_And_Configured_Paths()
    {
        var producer = Compilation("""
            public class Envelope { public Details Password { get; set; } }
            public class Details { public string Secret { get; set; } }
            """).WithAssemblyName("ExternalModels");
        using var stream = new MemoryStream();
        producer.Emit(stream).Success.Should().BeTrue();
        var consumer = Compilation("""
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog { void Add(string key, object value); }
            }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log, Envelope value)
                {
                    // DiagnosticNameExemption: Model.Password | Only the parent name.
                    log.Add("Model", value);
                    // DiagnosticNameExemption: Model.Password | Parent name.
                    // DiagnosticNameExemption: Model.Password.Secret | Child name.
                    log.Add("Model", value);
                    // DiagnosticNameExemption: Protected.Password | Cannot waive a configured path.
                    log.Add("Protected", value);
                }
            }
            """).AddReferences(MetadataReference.CreateFromImage(stream.ToArray()));
        var diagnostics = await Analyze(consumer, new ProjectDiagnosticRulesTests.RuleFile("data-classification-rules.json",
            """{"sensitiveFieldPaths":["Protected.Password"]}"""));

        diagnostics.Should().HaveCount(3);
        diagnostics.Should().ContainSingle(item => item.Id == "DRN0024");
        diagnostics.Should().ContainSingle(item => item.GetMessage().Contains("Model.Password.Secret", StringComparison.Ordinal));
        diagnostics.Should().ContainSingle(item => item.GetMessage().Contains("Protected.Password", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Comments_Should_Exempt_Only_Exact_Names_In_The_Next_Statement()
    {
        var diagnostics = await Analyze(Compilation("""
            using Microsoft.Extensions.Logging;
            public class Caller
            {
                public void Run(ILogger log, int count, string value)
                {
                    // DiagnosticNameExemption: TokenCount | Numeric metric, not a credential.
                    log.LogInformation("{TokenCount}", count);
                    log.LogInformation("Again {TokenCount}", count);
                    // DiagnosticNameExemption: tokencount | Wrong casing does not match.
                    log.LogInformation("Case {TokenCount}", count);
                    // DiagnosticNameExemption: TokenCount | Only one name is exempt.
                    log.LogInformation("{TokenCount} {Password}", count, value);
                    // DiagnosticNameExemption: TokenCount | A blank line detaches this comment.

                    log.LogInformation("Detached {TokenCount}", count);
                }
            }
            """));
        diagnostics.Should().HaveCount(6);
        diagnostics.Count(item => item.Id == "DRN0024").Should().Be(2);
        diagnostics.Where(item => item.Id == "DRN0022").Select(item => item.Location.SourceTree!.GetText().ToString(item.Location.SourceSpan))
            .Should().BeEquivalentTo("log.LogInformation(\"Again {TokenCount}\", count)",
                "log.LogInformation(\"Case {TokenCount}\", count)",
                "log.LogInformation(\"{TokenCount} {Password}\", count, value)",
                "log.LogInformation(\"Detached {TokenCount}\", count)");
        diagnostics.Where(item => item.Id == "DRN0022").Should().OnlyContain(item => item.GetMessage().Contains("immediately above this statement", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("// DiagnosticNameExemption: TokenCount")]
    [InlineData("// DiagnosticNameExemption: TokenCount | ")]
    [InlineData("// DiagnosticNameExemption: | Numeric metric.")]
    [InlineData("// DiagnosticNameExemption TokenCount | Missing colon.")]
    [InlineData("// DiagnosticNameExemption: * | No wildcard exemptions.")]
    public async Task Malformed_Comments_Should_Explain_The_Syntax_And_Keep_The_Capture_Error(string comment)
    {
        var diagnostics = await Analyze(Compilation($$"""
            using Microsoft.Extensions.Logging;
            public class Caller
            {
                public void Run(ILogger log, int count)
                {
                    {{comment}}
                    log.LogInformation("{TokenCount}", count);
                }
            }
            """));
        diagnostics.Should().HaveCount(2);
        diagnostics.Should().ContainSingle(item => item.Id == "DRN0022");
        var invalid = diagnostics.Should().ContainSingle(item => item.Id == "DRN0023").Which;
        invalid.GetMessage().Should().Contain("ExactName | Nonempty justification");
        invalid.Location.SourceTree!.GetText().ToString(invalid.Location.SourceSpan).Should().Be(comment);
    }

    [Fact]
    public async Task Comments_Should_Not_Exempt_Classified_Values_Or_Configured_Paths()
    {
        var diagnostics = await Analyze(Compilation("""
            using Microsoft.Extensions.Logging;
            using DRN.Framework.SharedKernel.Attributes;
            using System.Collections.Generic;
            public class Input { [Sensitive] public string Value => "private"; }
            public class Caller
            {
                public void Run(ILogger log, Input input, string value)
                {
                    // DiagnosticNameExemption: Detail | This cannot waive source classification.
                    log.LogInformation("{Detail}", input.Value);
                    // DiagnosticNameExemption: Payload | This cannot waive configured paths.
                    log.LogInformation("{Payload}", value);
                    // DiagnosticNameExemption: Payload | Scope paths remain protected too.
                    log.BeginScope(new Dictionary<string, object> { ["Payload"] = value });
                    // DiagnosticNameExemption: TokenCount | The value is classified, not a count.
                    log.BeginScope(new Dictionary<string, object> { ["TokenCount"] = input.Value });
                }
            }
            """), new ProjectDiagnosticRulesTests.RuleFile("data-classification-rules.json",
            """{"sensitiveFieldPaths":["Payload"]}"""));
        diagnostics.Should().HaveCount(7);
        diagnostics.Count(item => item.Id == "DRN0024").Should().Be(3);
        diagnostics.Count(item => item.Id == "DRN0022").Should().Be(4);
        diagnostics.Count(item => item.GetMessage().Contains("cannot waive this classification", StringComparison.Ordinal)).Should().Be(2);
        diagnostics.Count(item => item.GetMessage().Contains("cannot override configured paths", StringComparison.Ordinal)).Should().Be(2);
    }

    [Fact]
    public async Task Scope_Comments_Should_Work_On_Using_Declarations_Without_Leaking_Into_Lambdas()
    {
        var diagnostics = await Analyze(Compilation("""
            using Microsoft.Extensions.Logging;
            using System.Collections.Generic;
            public class Caller
            {
                public void Run(ILogger log, int count)
                {
                    // DiagnosticNameExemption: TokenCount | Numeric metric.
                    using var scope = log.BeginScope(new Dictionary<string, object> { ["TokenCount"] = count });
                    // DiagnosticNameExemption: TokenCount | This belongs to the declaration, not the callback.
                    System.Action callback = () => log.LogInformation("{TokenCount}", count);
                }
            }
            """));
        diagnostics.Should().HaveCount(2);
        diagnostics.Should().ContainSingle(item => item.Id == "DRN0022");
        diagnostics.Should().ContainSingle(item => item.Id == "DRN0024");
    }

    [Fact]
    public async Task Unused_Comments_Should_Warn_Without_Warning_For_Explicit_Attributes_Or_Reasons()
    {
        var compilation = Compilation("""
            using DRN.Framework.SharedKernel.Attributes;
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog
                {
                    void Add(string key, object value);
                    void Add(string key, object value, string reason);
                }
            }
            public class Model
            {
                [Sensitive(false)] public int Count { get; set; }
                [Sensitive] public string Value { get; set; }
            }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log)
                {
                    log.Add("Count", 1, "Reviewed metric.");
                    // DiagnosticNameExemption: Count | Harmless name.
                    log.Add("Count", 1);
                    // DiagnosticNameExemption: TokenCount | Duplicate comment.
                    // DiagnosticNameExemption: TokenCount | Reviewed metric.
                    log.Add("TokenCount", 1);
                }
            }
            """);
        var diagnostics = await Analyze(compilation);
        diagnostics.Should().HaveCount(2).And.OnlyContain(item =>
            item.Id == "DRN0024" && item.Severity == DiagnosticSeverity.Warning);
        diagnostics.Select(item => item.Location.SourceTree!.GetText().ToString(item.Location.SourceSpan))
            .Should().BeEquivalentTo("// DiagnosticNameExemption: Count | Harmless name.",
                "// DiagnosticNameExemption: TokenCount | Duplicate comment.");
        (await Analyze(compilation)).Should().HaveCount(2);
    }

    [Fact]
    public async Task Comment_Usage_Should_Be_Isolated_Per_Compilation()
    {
        var compilation = Compilation("""
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog { void Add(string key, object value); }
            }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log)
                {
                    // DiagnosticNameExemption: Metric | Reviewed metric.
                    log.Add("Metric", 1);
                }
            }
            """);
        var rules = new ProjectDiagnosticRulesTests.RuleFile("data-classification-rules.json",
            """{"secretFields":["metric"]}""");
        (await Analyze(compilation, rules)).Should().BeEmpty();
        (await Analyze(compilation)).Should().ContainSingle().Which.Id.Should().Be("DRN0024");
        (await Analyze(compilation, rules)).Should().BeEmpty();
    }

    [Fact]
    public async Task Concurrent_Compilations_Should_Not_Share_Comment_Usage()
    {
        var compilation = Compilation("""
            using Microsoft.Extensions.Logging;
            public class Caller
            {
                public void Run(ILogger log, int value)
                {
                    // DiagnosticNameExemption: Metric | Reviewed metric.
                    log.LogInformation("{Metric}", value);
                }
            }
            """);
        var analyzer = new DiagnosticClassificationAnalyzer();
        var rules = new ProjectDiagnosticRulesTests.RuleFile("data-classification-rules.json",
            """{"secretFields":["metric"]}""");
        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(async index =>
        {
            var configured = index % 2 == 0;
            var diagnostics = await compilation.WithAnalyzers([analyzer],
                new AnalyzerOptions(configured ? [rules] : [])).GetAnalyzerDiagnosticsAsync();
            return (configured, diagnostics);
        }));
        foreach (var (configured, diagnostics) in results)
        {
            if (configured) diagnostics.Should().BeEmpty();
            else diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRN0024");
        }
    }

    [Fact]
    public async Task Generated_Comments_Should_Not_Report_Unused_Warnings()
    {
        var diagnostics = await Analyze(Compilation("""
            // <auto-generated/>
            // DiagnosticNameExemption: Count | Generated comment.
            public class Generated { }
            """));
        diagnostics.Should().BeEmpty();
    }
}
