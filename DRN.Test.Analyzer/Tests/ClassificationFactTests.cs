using System.Diagnostics.CodeAnalysis;
using static DRN.Test.Analyzer.Tests.DiagnosticClassificationAnalyzerTests;

namespace DRN.Test.Analyzer.Tests;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public sealed class ClassificationFactTests
{
    [Fact]
    public void Shared_Attribute_Usage_Should_Follow_The_Current_Compilation()
    {
        const string source = """
            [System.AttributeUsage(System.AttributeTargets.All, Inherited = false, AllowMultiple = true)]
            public class ParentAttribute : System.Attribute;
            public class ChildAttribute : ParentAttribute;
            """;
        var original = Compilation(source);
        var cache = AttributeUsageFacts.Cache.For(original);
        AttributeUsageFacts.Cache.For(original).Should().BeSameAs(cache);
        var child = original.GetTypeByMetadataName("ChildAttribute")!;
        cache.Get(child, CancellationToken.None).Should().Be(new AttributeUsageFacts(false, true));
        Action canceled = () => cache.Get(child, new CancellationToken(canceled: true));
        canceled.Should().Throw<OperationCanceledException>();

        var changed = original.ReplaceSyntaxTree(original.SyntaxTrees.Single(),
            CSharpSyntaxTree.ParseText(source.Replace("Inherited = false", "Inherited = true", StringComparison.Ordinal)));
        var updated = AttributeUsageFacts.Cache.For(changed);
        updated.Should().NotBeSameAs(cache);
        updated.Get(changed.GetTypeByMetadataName("ChildAttribute")!, CancellationToken.None).Should().Be(new AttributeUsageFacts(true, true));
    }

    [Fact]
    public void Dictionary_Type_Facts_Should_Include_Contracts_And_Observe_Cancellation()
    {
        var compilation = Compilation("public class Lookalike { public void Add(string key, object value) { } }");
        var facts = new DictionaryCaptureAnalysis.DictionaryTypeFacts();
        var key = compilation.GetSpecialType(SpecialType.System_String);
        var value = compilation.GetSpecialType(SpecialType.System_Object);
        var dictionary = compilation.GetTypeByMetadataName("System.Collections.Generic.Dictionary`2")!.Construct(key, value);
        var mutable = compilation.GetTypeByMetadataName("System.Collections.Generic.IDictionary`2")!.Construct(key, value);
        var readOnly = compilation.GetTypeByMetadataName("System.Collections.Generic.IReadOnlyDictionary`2")!.Construct(key, value);
        var nonGeneric = compilation.GetTypeByMetadataName("System.Collections.IDictionary")!;
        var lookalike = compilation.GetTypeByMetadataName("Lookalike")!;
        Parallel.For(0, 8, _ =>
        {
            facts.IsDictionary(dictionary, CancellationToken.None).Should().BeTrue();
            facts.IsDictionary(mutable, CancellationToken.None).Should().BeTrue();
            facts.IsDictionary(readOnly, CancellationToken.None).Should().BeTrue();
            facts.IsDictionary(nonGeneric, CancellationToken.None).Should().BeTrue();
            facts.IsDictionary(lookalike, CancellationToken.None).Should().BeFalse();
        });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Action hit = () => facts.IsDictionary(dictionary, cancellation.Token);
        Action miss = () => facts.IsDictionary(key, cancellation.Token);
        hit.Should().Throw<OperationCanceledException>();
        miss.Should().Throw<OperationCanceledException>();
        facts.IsDictionary(key, CancellationToken.None).Should().BeFalse();
    }

    [Fact]
    public void Cached_Classification_Details_Should_Preserve_Exemptions_Inheritance_And_Receiver_Identity()
    {
        var compilation = Compilation("""
            using System;
            using DRN.Framework.SharedKernel.Attributes;
            [AttributeUsage(AttributeTargets.Class, Inherited = false)]
            public class LocalPersonalAttribute : PersonalAttribute;
            [LocalPersonal] public class Classified;
            public class Child : Classified;
            public interface IInput { [Personal] string Value { get; } }
            public class Parent
            {
                public string Value => "";
                [Sensitive(false)] public string Status => "";
            }
            public class Receiver : Parent, IInput;
            public class Separate : Parent, IInput { string IInput.Value => ""; }
            """);
        var contract = CreateContract(compilation);
        var parent = compilation.GetTypeByMetadataName("Parent")!;
        var property = (IPropertySymbol)parent.GetMembers("Value").Single();
        var exempt = (IPropertySymbol)parent.GetMembers("Status").Single();
        var receiver = compilation.GetTypeByMetadataName("Receiver")!;
        Parallel.For(0, 8, _ =>
        {
            contract.Positive(compilation.GetTypeByMetadataName("Classified")!, CancellationToken.None).Should().BeTrue();
            contract.Positive(compilation.GetTypeByMetadataName("Child")!, CancellationToken.None).Should().BeFalse();
            contract.Exempt(exempt).Should().BeTrue();
            contract.Exempt(property).Should().BeFalse();
            contract.PositiveInterfaceProperty(receiver, property, CancellationToken.None).Should().BeTrue();
            contract.PositiveInterfaceProperty(parent, property, CancellationToken.None).Should().BeFalse();
            contract.PositiveInterfaceProperty(compilation.GetTypeByMetadataName("Separate")!, property, CancellationToken.None).Should().BeFalse();
        });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Action exemptionHit = () => contract.Exempt(exempt, cancellation.Token);
        Action receiverHit = () => contract.PositiveInterfaceProperty(receiver, property, cancellation.Token);
        exemptionHit.Should().Throw<OperationCanceledException>();
        receiverHit.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void Cached_Interface_Scan_Exhaustion_Should_Remain_Incomplete_And_Observe_Cancellation()
    {
        var members = string.Join("\n", Enumerable.Range(0, LoggingCaptureAnalysis.MaxValues + 1)
            .Select(index => $"string P{index} {{ get; }}"));
        var implementations = string.Join("\n", Enumerable.Range(0, LoggingCaptureAnalysis.MaxValues + 1)
            .Select(index => $"public abstract string P{index} {{ get; }}"));
        var compilation = Compilation($$"""
            public interface IInput { {{members}} }
            public abstract class Input : IInput
            {
                {{implementations}}
                public string Value => "ordinary";
            }
            """);
        var contract = CreateContract(compilation);
        var receiver = compilation.GetTypeByMetadataName("Input")!;
        var property = (IPropertySymbol)receiver.GetMembers("Value").Single();
        Parallel.For(0, 8, _ =>
            contract.PositiveInterfaceProperty(receiver, property, CancellationToken.None).Should().BeNull());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Action hit = () => contract.PositiveInterfaceProperty(receiver, property, cancellation.Token);
        hit.Should().Throw<OperationCanceledException>();
        contract.PositiveInterfaceProperty(receiver, property, CancellationToken.None).Should().BeNull();
    }

    [Fact]
    public void Cached_Model_Shapes_Should_Preserve_Constructed_Elements_And_Nullable_Scalars()
    {
        var compilation = Compilation("public struct Model;");
        var contract = CreateContract(compilation);
        var model = compilation.GetTypeByMetadataName("Model")!;
        var list = compilation.GetTypeByMetadataName("System.Collections.Generic.List`1")!;
        var integer = compilation.GetSpecialType(SpecialType.System_Int32);
        contract.ModelType(list.Construct(model), CancellationToken.None).ListElement.Should().Be(model);
        contract.ModelType(list.Construct(integer), CancellationToken.None).ListElement.Should().Be(integer);
        contract.ModelType(list.Construct(model), CancellationToken.None).SkipProperties.Should().BeFalse();
        contract.ModelType(list.Construct(model), CancellationToken.None).Model.Should().BeFalse();
        var nullable = compilation.GetTypeByMetadataName("System.Nullable`1")!;
        contract.ModelType(nullable.Construct(model), CancellationToken.None).Model.Should().BeTrue();
        contract.ModelType(nullable.Construct(compilation.GetTypeByMetadataName("System.TimeSpan")!), CancellationToken.None)
            .TimeSpan.Should().BeTrue();
        var dictionary = compilation.GetTypeByMetadataName("System.Collections.Generic.Dictionary`2")!.Construct(integer, model);
        contract.ModelType(dictionary, CancellationToken.None).SkipProperties.Should().BeTrue();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Action hit = () => contract.ModelType(dictionary, cancellation.Token);
        Action miss = () => contract.ModelType(model, cancellation.Token);
        hit.Should().Throw<OperationCanceledException>();
        miss.Should().Throw<OperationCanceledException>();
        contract.ModelType(model, CancellationToken.None).Model.Should().BeTrue();
    }

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
        var classified = contract.DeclaredProperties(model.Construct(credential), CancellationToken.None).Single();
        var plain = contract.DeclaredProperties(model.Construct(compilation.GetSpecialType(SpecialType.System_Int32)), CancellationToken.None).Single();

        contract.Positive(credential, CancellationToken.None).Should().BeTrue();
        contract.Positive(nullable, CancellationToken.None).Should().BeTrue();
        contract.Accepted(classified, CancellationToken.None).Should().BeTrue();
        contract.Accepted(plain, CancellationToken.None).Should().BeFalse();
        contract.Accepted(classified, CancellationToken.None).Should().BeTrue();
        contract.Accepted(plain, CancellationToken.None).Should().BeFalse();

        var changed = Compilation(source.Replace("[Sensitive] public struct", "public struct", StringComparison.Ordinal));
        CreateContract(changed).Positive(changed.GetTypeByMetadataName("Credential")!, CancellationToken.None).Should().BeFalse();
        contract.Positive(credential, CancellationToken.None).Should().BeTrue();
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
        var properties = contract.DeclaredProperties(model, CancellationToken.None);
        properties.Select(property => property.Name).Should().Equal("Value", "Last");
        contract.DeclaredProperties(model, CancellationToken.None).Equals(properties).Should().BeTrue();
        contract.DeclaredProperties(model.BaseType!, CancellationToken.None).Select(property => property.Name).Should().Equal("Value", "Tail");
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

        diagnostics.Should().HaveCount(2).And.OnlyContain(item => item.Id == "DRNCLASSIFICATION003");
        diagnostics.Select(item => item.Location.SourceTree!.GetText().ToString(item.Location.SourceSpan))
            .Should().BeEquivalentTo("log.AddProperties(\"First\", value)", "log.AddProperties(\"Second\", value)");
    }

    [Fact]
    public async Task Cached_Property_Prefix_Should_Preserve_The_Work_Limit_For_Every_Capture()
    {
        var properties = string.Join("\n", Enumerable.Range(0, ModelCaptureAnalysis.MaxModelWork + 1)
            .Select(index => $"[Sensitive] public int P{index} => 0;"));
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

        diagnostics.Should().HaveCount(2).And.OnlyContain(item => item.Id == "DRNCLASSIFICATION011");
        diagnostics.Select(item => item.Location.SourceTree!.GetText().ToString(item.Location.SourceSpan))
            .Should().BeEquivalentTo("log.AddProperties(\"First\", value)", "log.AddProperties(\"Second\", value)");
    }

    [Fact]
    public void Nullable_Annotated_Reference_And_Collection_Shapes_Should_Match_Non_Nullable_Facts()
    {
        var compilation = Compilation("""
            #nullable enable
            using System;
            using System.Collections;
            using System.Collections.Generic;
            using DRN.Framework.SharedKernel.Attributes;
            using DRN.Framework.Utils.Logging;

            [Sensitive] public sealed class SecretItem;

            public sealed class Shapes
            {
                public Uri Address { get; set; } = new("https://example.test/");
                public Uri? NullableAddress { get; set; }
                public ScopeEvent? Event { get; set; }
                public IEnumerable? Sequence { get; set; }
                public IList<SecretItem> SecretList { get; set; } = new List<SecretItem>();
                public IDictionary? Dict { get; set; }
                public Dictionary<string, object>? GenericDict { get; set; }
            }
            """)
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable))
            .AddReferences(AnalyzerTestCompilation.UtilsReference);
        var contract = CreateContract(compilation);
        var dictFacts = new DictionaryCaptureAnalysis.DictionaryTypeFacts();
        var shapes = compilation.GetTypeByMetadataName("Shapes")!;
        var secretItem = compilation.GetTypeByMetadataName("SecretItem")!;
        var address = ((IPropertySymbol)shapes.GetMembers("Address").Single()).Type;
        var nullableAddress = ((IPropertySymbol)shapes.GetMembers("NullableAddress").Single()).Type;
        var scopeEvent = ((IPropertySymbol)shapes.GetMembers("Event").Single()).Type;
        var sequence = ((IPropertySymbol)shapes.GetMembers("Sequence").Single()).Type;
        var secretList = ((IPropertySymbol)shapes.GetMembers("SecretList").Single()).Type;
        var dict = ((IPropertySymbol)shapes.GetMembers("Dict").Single()).Type;
        var genericDict = ((IPropertySymbol)shapes.GetMembers("GenericDict").Single()).Type;

        nullableAddress.NullableAnnotation.Should().Be(NullableAnnotation.Annotated);
        contract.ModelType(nullableAddress, CancellationToken.None).Should().Be(contract.ModelType(address, CancellationToken.None));
        contract.ModelType(scopeEvent, CancellationToken.None).Should().Be(contract.ModelType(scopeEvent.WithNullableAnnotation(NullableAnnotation.NotAnnotated), CancellationToken.None));
        contract.ModelType(sequence, CancellationToken.None).Should().Be(contract.ModelType(sequence.WithNullableAnnotation(NullableAnnotation.NotAnnotated), CancellationToken.None));
        contract.ModelType(secretList, CancellationToken.None).ListElement.Should().Be(secretItem);
        dictFacts.IsDictionary(dict, CancellationToken.None).Should().BeTrue();
        dictFacts.IsDictionary(genericDict, CancellationToken.None).Should().BeTrue();
    }

    private static ClassificationContract CreateContract(Compilation compilation)
    {
        compilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        return new ClassificationContract(
            compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Attributes.SensitiveAttribute")!,
            compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Attributes.PersonalAttribute")!, compilation);
    }
}
