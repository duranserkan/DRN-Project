using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Emit;
using DRN.Test.Shared;
using static DRN.Test.Analyzer.Tests.DiagnosticClassificationAnalyzerTests;

namespace DRN.Test.Analyzer.Tests;

public sealed class ProjectDiagnosticRulesTests
{
    internal static string Fixture()
    {
        using var stream = typeof(ProjectDiagnosticRulesTests).Assembly.GetManifestResourceStream("ProjectDiagnosticRules.json")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    internal sealed class RuleFile(string path, string? text) : AdditionalText
    {
        public override string Path => path;
        public override SourceText? GetText(CancellationToken cancellationToken = default) => text == null ? null : SourceText.From(text);
    }

    [Fact]
    public async Task Reference_Assembly_Catalogs_Should_Include_Only_Transitive_SharedKernel_Consumers()
    {
        var dependency = Reference("RuleDependency", """
            public class Contract { public DRN.Framework.SharedKernel.Attributes.SensitiveAttribute Marker => new(); }
            """, "{\"personalFields\":[\"inheritedMember\"]}");
        var middle = Reference("RuleMiddle", "public class Middle : Contract;",
            "{\"secretFields\":[\"middleProof\"]}", true, dependency);
        var unrelated = Reference("RuleUnrelated", "public class Unrelated;", "{\"personalFields\":[\"unrelatedMember\"]}", false);
        var compilation = DiagnosticClassificationAnalyzerTests.Compilation("""
            public class Model
            {
                public string InheritedMember { get; set; } = "";
                public string MiddleProof { get; set; } = "";
                public string UnrelatedMember { get; set; } = "";
            }
            """).AddReferences(dependency, middle, unrelated);
        var diagnostics = await Analyze(compilation);
        diagnostics.Should().HaveCount(2).And.OnlyContain(item => item.Id == "DRN0015");
        diagnostics.Should().Contain(item => item.GetMessage().Contains("InheritedMember"));
        diagnostics.Should().Contain(item => item.GetMessage().Contains("MiddleProof"));
        (await Analyze(DiagnosticClassificationAnalyzerTests.Compilation("public class Model { public string InheritedMember => \"\"; }")))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task Invalid_Inherited_Catalogs_Should_Report_Configuration_Errors()
    {
        var dependency = Reference("InvalidRuleDependency",
            "public class Contract { public DRN.Framework.SharedKernel.Attributes.SensitiveAttribute Marker => new(); }", "{");
        var diagnostics = await Analyze(DiagnosticClassificationAnalyzerTests.Compilation("public class Model;").AddReferences(dependency));
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRN0021");
    }

    [Fact]
    public async Task Own_Assembly_Metadata_Should_Use_The_Same_Catalog_Parser()
    {
        var source = "[assembly: DRN.Framework.SharedKernel.Attributes.DataClassificationRules(" +
                     Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(Fixture(), true) + ")]\n" +
                     "public class Model { public string MembershipNumber => \"\"; }";
        var diagnostics = await Analyze(DiagnosticClassificationAnalyzerTests.Compilation(source));
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRN0015");
    }

    private static PortableExecutableReference Reference(string name, string source, string json, bool drnCatalog = true,
        params MetadataReference[] dependencies)
    {
        var metadata = (drnCatalog ? "[assembly: DRN.Framework.SharedKernel.Attributes.DataClassificationRules(" :
                           "[assembly: System.Reflection.AssemblyMetadata(\"DRN.DataClassificationRules\", ") +
                       Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(json, true) + ")]\n";
        var compilation = DiagnosticClassificationAnalyzerTests.Compilation(metadata + source).WithAssemblyName(name).AddReferences(dependencies);
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream, options: new EmitOptions(metadataOnly: true, includePrivateMembers: false));
        result.Success.Should().BeTrue(string.Join(Environment.NewLine, result.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    [Fact]
    public async Task Project_Rules_Should_Extend_Builtins_Without_Leaking_Across_Compilations()
    {
        var compilation = DiagnosticClassificationAnalyzerTests.Compilation("""
            public class Model
            {
                public string Membership_Number { get; set; } = "";
                public string Password { get; set; } = "";
            }
            """);
        var results = await Task.WhenAll(
            Analyze(compilation, new RuleFile("data-classification-rules.json", Fixture())),
            Analyze(compilation),
            Analyze(compilation, new RuleFile("unrelated.json", Fixture())),
            Analyze(compilation, new RuleFile("data-classification-rules.json", "{}")));
        results[0].Should().HaveCount(2).And.OnlyContain(item => item.Id == "DRN0015");
        foreach (var diagnostics in results.Skip(1))
            diagnostics.Should().ContainSingle().Which.GetMessage().Should().Contain("Password");
        (await Analyze(compilation, new RuleFile("diagnostic-rules.json", Fixture()))).Should().HaveCount(2);
    }

    [Fact]
    public async Task Custom_Rules_Should_Cover_Anonymous_Scalar_And_Dictionary_Captures()
    {
        var compilation = DiagnosticClassificationAnalyzerTests.Compilation("""
            using System.Collections.Generic;
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog { void Add(string key, object value); }
            }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log)
                {
                    var model = new { MembershipNumber = "member" };
                    log.Add("ChallengeResponse", "challenge");
                    log.Add("Payload.PrivateValue", "private");
                    log.Add("Data", new Dictionary<string, object> { ["MembershipNumber"] = "member" });
                    log.Add("Payload", new Dictionary<string, object> { ["PrivateValue"] = "private" });
                    log.Add("Root", new Dictionary<string, object>
                    {
                        ["Payload"] = new Dictionary<string, object> { ["PrivateValue"] = "private" }
                    });
                    var shared = new Dictionary<string, object> { ["PrivateValue"] = "private" };
                    log.Add("Root", new Dictionary<string, object> { ["Other"] = shared, ["Payload"] = shared });
                }
            }
            """);
        var diagnostics = await Analyze(compilation, new RuleFile("data-classification-rules.json", Fixture()));
        diagnostics.Count(item => item.Id == "DRN0019").Should().Be(1);
        diagnostics.Count(item => item.Id == "DRN0017").Should().Be(2);
        diagnostics.Count(item => item.Id == "DRN0020").Should().Be(4);
        diagnostics.Should().HaveCount(7);
    }

    [Fact]
    public async Task Shared_Dictionary_Graphs_Should_Be_Bounded_Without_Disabling_Later_Captures()
    {
        var declarations = new System.Text.StringBuilder("var level0 = new Dictionary<string, object>();\n");
        for (var level = 1; level <= 10; level++)
        {
            declarations.Append($"var level{level} = new Dictionary<string, object> {{ ");
            for (var branch = 0; branch < 8; branch++)
                declarations.Append($"[\"Branch{branch}\"] = level{level - 1}, ");
            declarations.AppendLine("};");
        }
        var compilation = DiagnosticClassificationAnalyzerTests.Compilation($$"""
            using System.Collections.Generic;
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog { void Add(string key, object value); }
            }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log)
                {
                    {{declarations}}
                    log.Add("Root", level10);
                    log.Add("Payload", new Dictionary<string, object> { ["PrivateValue"] = "private" });
                }
            }
            """);
        compilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var diagnostics = await compilation.WithAnalyzers([new DiagnosticClassificationAnalyzer()],
                new AnalyzerOptions([new RuleFile("data-classification-rules.json", Fixture())]))
            .GetAnalyzerDiagnosticsAsync(cancellation.Token).WaitAsync(cancellation.Token);
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRN0020");
        diagnostics.Single().GetMessage().Should().Contain("Payload.PrivateValue");
    }

    [Fact]
    public async Task Shared_Model_Graphs_Should_Be_Bounded_Without_Disabling_Later_Captures()
    {
        var declarations = new System.Text.StringBuilder("public class Level0 { public int Value { get; set; } }\n");
        for (var level = 1; level <= 10; level++)
        {
            declarations.Append($"public class Level{level} {{ ");
            for (var branch = 0; branch < 8; branch++)
                declarations.Append($"public Level{level - 1} Branch{branch} {{ get; set; }} ");
            declarations.AppendLine("}");
        }
        var compilation = Compilation($$"""
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog { void Add(string key, object value); }
            }
            {{declarations}}
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log, Level10 large, Level0 small)
                {
                    log.Add("Root", large);
                    log.Add("Payload", small);
                }
            }
            """);
        compilation.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var diagnostics = await compilation.WithAnalyzers([new DiagnosticClassificationAnalyzer()],
                new AnalyzerOptions([new RuleFile("data-classification-rules.json", """{"sensitiveFieldPaths":["Payload.Value"]}""")]))
            .GetAnalyzerDiagnosticsAsync(cancellation.Token).WaitAsync(cancellation.Token);
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRN0017");
        diagnostics.Single().GetMessage().Should().Contain("Payload.Value");
    }

    [Fact]
    public async Task Shared_Model_Types_Should_Be_Checked_At_Each_Configured_Path()
    {
        var diagnostics = await Analyze(Compilation("""
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog { void Add(string key, object value); }
            }
            public class Leaf { public string Value { get; set; } = ""; }
            public class Model
            {
                public Leaf Left { get; set; }
                public Leaf Right { get; set; }
            }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log, Model model) => log.Add("Root", model);
            }
            """), new RuleFile("data-classification-rules.json",
            """{"sensitiveFieldPaths":["Root.Left.Value","Root.Right.Value"]}"""));
        diagnostics.Should().HaveCount(2).And.OnlyContain(diagnostic => diagnostic.Id == "DRN0017");
        diagnostics.Select(diagnostic => diagnostic.GetMessage()).Should().Contain(message => message.Contains("Root.Left.Value"))
            .And.Contain(message => message.Contains("Root.Right.Value"));
    }

    [Fact]
    public async Task Shared_Dictionary_Locals_Should_Not_Spend_Their_Budget_Rescanning_Unrelated_Operations()
    {
        var unrelated = string.Concat(Enumerable.Repeat("System.Console.WriteLine(0);\n", 1000));
        var entries = string.Join(", ", Enumerable.Range(0, 12).Select(index => $"[\"Branch{index}\"] = shared"));
        var diagnostics = await Analyze(Compilation($$"""
            using System.Collections.Generic;
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog { void Add(string key, object value); }
            }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log)
                {
                    {{unrelated}}
                    var shared = new Dictionary<string, object>();
                    shared["Value"] = "private";
                    var root = new Dictionary<string, object> { {{entries}} };
                    log.Add("Root", root);
                    shared["LaterValue"] = "private";
                }
            }
            """), new RuleFile("data-classification-rules.json",
            """{"sensitiveFieldPaths":["Root.Branch11.Value","Root.Branch11.LaterValue"]}"""));
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRN0020");
        diagnostics.Single().GetMessage().Should().Contain("Root.Branch11.Value");
    }

    [Fact]
    public async Task Project_Field_Paths_Should_Check_Typed_Capture_Prefixes()
    {
        var compilation = DiagnosticClassificationAnalyzerTests.Compilation("""
            using DRN.Framework.SharedKernel.Attributes;
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog { void Add(string key, object value); }
            }
            public class Model
            {
                [Sensitive(false)] public string PrivateValue => "probe";
            }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log)
                {
                    log.Add("Payload", new Model());
                    log.Add("Other", new Model());
                }
            }
            """);
        var diagnostics = await Analyze(compilation, new RuleFile("data-classification-rules.json", Fixture()));
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRN0017");
        diagnostics.Single().GetMessage().Should().Contain("Payload.PrivateValue");
    }

    [Fact]
    public async Task Invalid_Rule_Files_Should_Report_Configuration_Errors_And_Keep_Builtins()
    {
        var compilation = DiagnosticClassificationAnalyzerTests.Compilation("public class Model { public string Password { get; set; } = \"\"; }");
        foreach (var json in InvalidDiagnosticRuleCases.Create().Cast<string?>().Prepend(null))
        {
            var diagnostics = await Analyze(compilation, new RuleFile("data-classification-rules.json", json));
            diagnostics.Should().HaveCount(2);
            diagnostics.Should().ContainSingle(item => item.Id == "DRN0015");
            diagnostics.Should().ContainSingle(item => item.Id == "DRN0021");
        }
        var duplicate = await Analyze(compilation, new RuleFile("data-classification-rules.json", "{}"),
            new RuleFile("diagnostic-rules.json", "{}"));
        duplicate.Should().ContainSingle(item => item.Id == "DRN0021");
    }
}
