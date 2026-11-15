using static DRN.Test.Analyzer.Tests.DiagnosticClassificationAnalyzerTests;

namespace DRN.Test.Analyzer.Tests;

public sealed class ClassificationFactTests
{
    [Fact]
    public void Classification_Facts_Should_Follow_Their_Compilation_And_Constructed_Type()
    {
        const string source = """
            using DRN.Framework.SharedKernel.Attributes;
            [Sensitive] public struct Credential { }
            public class Model<T> { public T Value { get; set; } }
            public class Input { public Credential? Value { get; set; } }
            """;
        var compilation = Compilation(source);
        var contract = CreateContract(compilation);
        var credential = compilation.GetTypeByMetadataName("Credential")!;
        var nullable = ((IPropertySymbol)compilation.GetTypeByMetadataName("Input")!.GetMembers("Value").Single()).Type;
        var model = compilation.GetTypeByMetadataName("Model`1")!;
        var classified = contract.DeclaredProperties(model.Construct(credential), default).Single();
        var plain = contract.DeclaredProperties(model.Construct(compilation.GetSpecialType(SpecialType.System_Int32)), default).Single();

        contract.Positive(credential, default).Should().BeTrue();
        contract.Positive(nullable, default).Should().BeTrue();
        contract.Accepted(classified, default).Should().BeTrue();
        contract.Accepted(plain, default).Should().BeFalse();
        contract.Accepted(classified, default).Should().BeTrue();
        contract.Accepted(plain, default).Should().BeFalse();

        var changed = Compilation(source.Replace("[Sensitive] public struct", "public struct", StringComparison.Ordinal));
        CreateContract(changed).Positive(changed.GetTypeByMetadataName("Credential")!, default).Should().BeFalse();
        contract.Positive(credential, default).Should().BeTrue();
    }

    [Fact]
    public void Property_Facts_Should_Reuse_Ordered_Declared_Properties_Without_Removing_Hidden_Base_Members()
    {
        var compilation = Compilation("""
            public class Parent { public int Value { get; set; } public int Tail { get; set; } }
            public class Model : Parent
            {
                public new string Value { get; set; } = "";
                public static int StaticValue { get; set; }
                public int this[int index] => index;
                public int WriteOnly { private get; set; }
                public int Last { get; set; }
            }
            """);
        var contract = CreateContract(compilation);
        var model = compilation.GetTypeByMetadataName("Model")!;
        var properties = contract.DeclaredProperties(model, default);
        properties.Select(property => property.Name).Should().Equal("Value", "Last");
        contract.DeclaredProperties(model, default).Equals(properties).Should().BeTrue();
        contract.DeclaredProperties(model.BaseType!, default).Select(property => property.Name).Should().Equal("Value", "Tail");
    }

    [Fact]
    public async Task Reused_Model_Facts_Should_Keep_Each_Capture_Path_And_Location()
    {
        var diagnostics = await Analyze(Compilation("""
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog { void AddProperties<T>(string prefix, T value) where T : class; }
            }
            public class Model { public string Value { get; set; } = ""; }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log, Model value)
                {
                    log.AddProperties("First", value);
                    log.AddProperties("Safe", value);
                    log.AddProperties("Second", value);
                }
            }
            """), new ProjectDiagnosticRulesTests.RuleFile("data-classification-rules.json",
            """{"sensitiveFieldPaths":["First.Value","Second.Value"]}"""));

        diagnostics.Should().HaveCount(2).And.OnlyContain(item => item.Id == "DRN0017");
        diagnostics.Select(item => item.Location.SourceTree!.GetText().ToString(item.Location.SourceSpan))
            .Should().BeEquivalentTo("log.AddProperties(\"First\", value)", "log.AddProperties(\"Second\", value)");
    }

    [Fact]
    public async Task Cached_Property_Prefix_Should_Preserve_The_Work_Limit_For_Every_Capture()
    {
        var properties = string.Join("\n", Enumerable.Range(0, ModelCaptureAnalysis.MaxModelWork + 1)
            .Select(index => $"[Sensitive] public int P{index} {{ get; set; }}"));
        var diagnostics = await Analyze(Compilation($$"""
            using DRN.Framework.SharedKernel.Attributes;
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog { void AddProperties<T>(string prefix, T value) where T : class; }
            }
            public class Model { {{properties}} }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log, Model value)
                {
                    log.AddProperties("First", value);
                    log.AddProperties("Second", value);
                }
            }
            """));

        diagnostics.Should().HaveCount(2).And.OnlyContain(item => item.Id == "DRN0025");
        diagnostics.Select(item => item.Location.SourceTree!.GetText().ToString(item.Location.SourceSpan))
            .Should().BeEquivalentTo("log.AddProperties(\"First\", value)", "log.AddProperties(\"Second\", value)");
    }

    private static ClassificationContract CreateContract(Compilation compilation)
    {
        compilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        return new ClassificationContract(
            compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Attributes.SensitiveAttribute")!,
            compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Attributes.PersonalAttribute")!);
    }
}
