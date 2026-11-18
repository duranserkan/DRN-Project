using System.Diagnostics.CodeAnalysis;
using DRN.Framework.SharedKernel.Analyzers.Conventions;
using Microsoft.CodeAnalysis.Emit;
using static DRN.Test.Analyzer.Tests.DiagnosticClassificationAnalyzerTests;

namespace DRN.Test.Analyzer.Tests;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public sealed class ConventionFactsTests
{
    [Fact]
    public void Cached_Generic_Facts_Should_Preserve_Constructed_Types_And_Observe_Cancellation()
    {
        var compilation = Compilation("public interface IBinding<T>; public class Model<T> : IBinding<T>;");
        var symbols = new ConventionSymbols(compilation);
        var definition = compilation.GetTypeByMetadataName("Model`1")!;
        var contract = compilation.GetTypeByMetadataName("IBinding`1")!;
        var integer = compilation.GetSpecialType(SpecialType.System_Int32);
        var text = compilation.GetSpecialType(SpecialType.System_String);
        var first = definition.Construct(integer);
        var second = definition.Construct(text);
        symbols.IsOpen(definition).Should().BeTrue();
        symbols.IsOpen(first).Should().BeFalse();
        symbols.IsOpen(second).Should().BeFalse();
        var firstBindings = symbols.Bindings(first, contract, CancellationToken.None);
        firstBindings.Should().ContainSingle().Which.TypeArguments.Should().ContainSingle().Which.Should().Be(integer);
        symbols.Bindings(second, contract, CancellationToken.None).Should().ContainSingle()
            .Which.TypeArguments.Should().ContainSingle().Which.Should().Be(text);
        symbols.Bindings(first, contract, CancellationToken.None).Equals(firstBindings).Should().BeTrue();
        symbols.Bindings(first, definition, CancellationToken.None).Should().ContainSingle().Which.Should().Be(first);
        symbols.Bindings(first, null, CancellationToken.None).Should().BeEmpty();

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Action openHit = () => symbols.IsOpen(first, cancellation.Token);
        Action bindingHit = () => symbols.Bindings(first, contract, cancellation.Token);
        var unbound = definition.ConstructUnboundGenericType();
        Action openMiss = () => symbols.IsOpen(unbound, cancellation.Token);
        Action bindingMiss = () => symbols.Bindings(definition, contract, cancellation.Token);
        openHit.Should().Throw<OperationCanceledException>();
        bindingHit.Should().Throw<OperationCanceledException>();
        openMiss.Should().Throw<OperationCanceledException>();
        bindingMiss.Should().Throw<OperationCanceledException>();
        symbols.IsOpen(unbound).Should().BeTrue();
        symbols.Bindings(definition, contract, CancellationToken.None).Should().ContainSingle()
            .Which.TypeArguments.Should().ContainSingle().Which.TypeKind.Should().Be(TypeKind.TypeParameter);
    }

    [Fact]
    public void Cached_Assembly_Metadata_Should_Preserve_Entries_And_Compilation_Isolation()
    {
        var first = Compilation("""
            [assembly: System.Reflection.AssemblyMetadata("Key", "First")]
            [assembly: System.Reflection.AssemblyMetadata("Key", "Second")]
            [assembly: System.Reflection.AssemblyMetadata("Empty", null)]
            public class Marker;
            """);
        var facts = Facts(first);
        var metadata = facts.AssemblyMetadata(first.Assembly);
        metadata.Select(pair => (pair.Key, pair.Value)).Should().Equal(
            ("Key", "First"), ("Key", "Second"), ("Empty", ""));
        facts.AssemblyMetadata(first.Assembly).Equals(metadata).Should().BeTrue();

        var second = Compilation("""
            [assembly: System.Reflection.AssemblyMetadata("Key", "Changed")]
            public class Marker;
            """);
        Facts(second).AssemblyMetadata(second.Assembly).Should().ContainSingle()
            .Which.Value.Should().Be("Changed");
    }

    [Theory]
    [DataInlineUnit("Layers/Storage")]
    [DataInlineUnit("Layers\\Storage")]
    public void Role_Mapping_Should_Normalize_Logical_Path_Separators(string prefix)
    {
        var compilation = Compilation("namespace Sample.Storage.QA; public class Model;");
        var facts = Facts(compilation, Role("Infra", "Sample.Storage", prefix));
        facts.Role(compilation.GetTypeByMetadataName("Sample.Storage.QA.Model")!)
            .Should().Be(("Infra", "Sample.Storage", "Layers/Storage"));
        facts.Diagnostics.Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public void External_Constructed_Ownership_Should_Use_The_Generic_Definition(bool contradictory)
    {
        var producer = Compilation("namespace External; public class Row<T>;").WithAssemblyName("External.Library");
        var secondOwner = contradictory ? "Billing" : "QA";
        var consumer = Compilation($$"""
            using DRN.Framework.SharedKernel.Attributes;
            [assembly: SourceKnownSubdomain(typeof(External.Row<int>), "QA", Reason = "External storage owner")]
            [assembly: SourceKnownSubdomain(typeof(External.Row<string>), "{{secondOwner}}", Reason = "External storage owner")]
            public class Consumer;
            """).AddReferences(Reference(producer, true));
        consumer.GetDiagnostics().Should().NotContain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var facts = Facts(consumer);
        var definition = consumer.GetTypeByMetadataName("External.Row`1")!;
        var constructed = definition.Construct(consumer.GetSpecialType(SpecialType.System_Int32));
        if (contradictory)
        {
            facts.Owner(constructed).Should().BeNull();
            facts.HasInvalidOwner(definition).Should().BeTrue();
            facts.Diagnostics.Should().ContainSingle().Which.GetMessage().Should().Contain("contradictory external ownership");
        }
        else
        {
            facts.Owner(constructed)!.Subdomain.Should().Be("QA");
            facts.Owner(definition)!.Subdomain.Should().Be("QA");
            facts.Diagnostics.Should().BeEmpty();
        }
    }

    [Fact]
    public void Cached_Locations_Should_Keep_Logical_Path_Order_And_Observe_Cancellation()
    {
        const string source = "namespace Sample.Infra.QA; public partial class Model;";
        var compilation = Compilation(source, "A.cs").AddSyntaxTrees(
            CSharpSyntaxTree.ParseText(source, path: "Z.cs"),
            CSharpSyntaxTree.ParseText("// <auto-generated/>\n" + source, path: "Generated.cs"));
        compilation.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        var inputs = new ConventionInputs([
            new("Path", "A.cs", ImmutableDictionary<string, string>.Empty.Add("LogicalPath", "Z.cs"), Location.None),
            new("Path", "Z.cs", ImmutableDictionary<string, string>.Empty.Add("LogicalPath", "A.cs"), Location.None)
        ], []);
        using var cancellation = new CancellationTokenSource();
        var facts = new ConventionFacts(compilation,
            new(true, "Sample.Infra", "Infra", "Sample.Infra", compilation.AssemblyName!, []),
            inputs, new Options(), cancellation.Token);
        var model = compilation.GetTypeByMetadataName("Sample.Infra.QA.Model")!;
        var generated = compilation.SyntaxTrees.Last();
        for (var i = 0; i < 2; i++)
        {
            facts.Location(model).SourceTree!.FilePath.Should().Be("Z.cs");
            facts.IsGenerated(generated).Should().BeTrue();
            facts.Role(model).Role.Should().Be("Infra");
            facts.AssemblyMetadata(compilation.Assembly).Should().BeEmpty();
        }
        cancellation.Cancel();
        Action location = () => facts.Location(model);
        Action generatedStatus = () => facts.IsGenerated(generated);
        Action role = () => facts.Role(model);
        Action metadata = () => facts.AssemblyMetadata(compilation.Assembly);
        location.Should().Throw<OperationCanceledException>();
        generatedStatus.Should().Throw<OperationCanceledException>();
        role.Should().Throw<OperationCanceledException>();
        metadata.Should().Throw<OperationCanceledException>();
    }

    [Theory]
    [InlineData("false", "// <auto-generated/>", "Model.g.cs", false)]
    [InlineData("True", "", "Model.cs", true)]
    [InlineData("", "", "Model.Designer.cs", true)]
    [InlineData("", "// <autogenerated/>", "Model.cs", true)]
    public void Generated_Source_Should_Honor_Explicit_Tree_Options(string setting, string header, string path, bool expected)
    {
        var compilation = Compilation(header + "\npublic class Model;", path);
        var facts = new ConventionFacts(compilation, new(true, "Sample.Domain", "Domain", "Sample.Domain", compilation.AssemblyName!, []),
            new([], []), new Options(setting), CancellationToken.None);
        facts.IsGenerated(compilation.SyntaxTrees.Single()).Should().Be(expected);
    }

    [Fact]
    public void Cached_Classifications_Should_Remain_Isolated_And_Allow_Retry_After_Cancellation()
    {
        var compilation = Compilation("namespace Sample.Infra.QA; public class Model : Microsoft.EntityFrameworkCore.DbContext;");
        compilation.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        var type = compilation.GetTypeByMetadataName("Sample.Infra.QA.Model")!;
        var symbols = new ConventionSymbols(compilation);
        symbols.IsContext(type, CancellationToken.None).Should().BeTrue();
        symbols.IsEntity(type, CancellationToken.None).Should().BeFalse();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Action warm = () => symbols.IsContext(type, cancellation.Token);
        Action cold = () => symbols.IsPersistenceType(type, cancellation.Token);
        warm.Should().Throw<OperationCanceledException>();
        cold.Should().Throw<OperationCanceledException>();
        symbols.IsPersistenceType(type, CancellationToken.None).Should().BeTrue();
        symbols.IsContext(type, CancellationToken.None).Should().BeTrue();

        var other = Compilation("namespace Sample.Infra.QA; public class Model;");
        new ConventionSymbols(other).IsContext(other.GetTypeByMetadataName("Sample.Infra.QA.Model")!, CancellationToken.None)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(true, false, 1)]
    [InlineData(true, true, 2)]
    public void Cached_Attribute_Usage_Should_Preserve_Inheritance_And_Multiplicity(bool inherited, bool multiple, int count)
    {
        var compilation = Compilation($$"""
            using System;
            [AttributeUsage(AttributeTargets.Class, Inherited = {{inherited.ToString().ToLowerInvariant()}}, AllowMultiple = {{multiple.ToString().ToLowerInvariant()}})]
            public class MarkerAttribute : Attribute;
            [Marker] public class Root;
            [Marker] public class Middle : Root;
            public class Leaf : Middle;
            """);
        compilation.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        var symbols = new ConventionSymbols(compilation);
        var target = compilation.GetTypeByMetadataName("MarkerAttribute")!;
        var leaf = compilation.GetTypeByMetadataName("Leaf")!;
        for (var i = 0; i < 2; i++)
            symbols.EffectiveAttributes(leaf, target, CancellationToken.None).Should().HaveCount(count);
    }

    [Fact]
    public void Contradictory_Unreasoned_Ownership_Should_Report_One_Issue_For_The_Target()
    {
        var compilation = Compilation("""
            namespace Sample.Infra.QA;
            [DRN.Framework.SharedKernel.Attributes.SourceKnownSubdomain("Other")]
            public class Category;
            """).WithAssemblyName("Sample.Infra");
        var location = Location.Create("DrnConventionInputs.g.cs", new(0, 1), new(new(0, 0), new(0, 1)));
        var facts = Facts(compilation, new ConventionInput("Ownership", "Sample.Infra.QA.Category",
            ImmutableDictionary<string, string>.Empty.Add("Subdomain", "QA").Add("Kind", "Entity"), location));
        var category = compilation.GetTypeByMetadataName("Sample.Infra.QA.Category")!;
        facts.Owner(category).Should().BeNull();
        facts.Owner(category).Should().BeNull();
        var diagnostic = facts.Diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNCONVENTIONINPUT001");
        diagnostic.GetMessage().Should().Contain("contradictory").And.Contain("Sample.Infra.QA.Category");
        diagnostic.Location.Should().Be(location);
        diagnostic.AdditionalLocations.Should().ContainSingle().Which.SourceTree.Should().Be(compilation.SyntaxTrees.Single());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Malformed_Referenced_Project_Identity_Should_Be_Reported_Once_For_Multiple_Requested_Types(bool emitted)
    {
        var producer = Compilation("""
            using System.Reflection;
            [assembly: AssemblyMetadata("DRN.Conventions.ProjectName", "Sample.Domain")]
            [assembly: AssemblyMetadata("DRN.Conventions.ProjectName", "Other.Domain")]
            [assembly: AssemblyMetadata("DRN.Conventions.RootNamespace", "Sample.Domain")]
            namespace Sample.Domain.QA;
            public class First;
            public class Second;
            """).WithAssemblyName("Sample.Domain");
        var consumer = Compilation("public class Consumer;").WithAssemblyName("Sample.Infra").AddReferences(Reference(producer, emitted));
        var facts = Facts(consumer);
        foreach (var name in new[] { "First", "Second", "First" })
            facts.Owner(consumer.GetTypeByMetadataName("Sample.Domain.QA." + name)!).Should().BeNull();
        var diagnostic = facts.Diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNCONVENTIONINPUT001");
        diagnostic.GetMessage().Should().Contain("MSBuildProjectName").And.Contain("contradictory");
        diagnostic.Location.Should().Be(Location.None);
    }

    [Theory]
    [InlineData("Role.Infra", "Sample.Domain.QA")]
    [InlineData("Role.Imaginary", "Sample.Other")]
    public void Malformed_Referenced_Role_Inventory_Should_Not_Supply_An_Owner(string key, string value)
    {
        var producer = Compilation($$"""
            using System.Reflection;
            [assembly: AssemblyMetadata("DRN.Conventions.ProjectName", "Sample.Domain")]
            [assembly: AssemblyMetadata("DRN.Conventions.RootNamespace", "Sample.Domain")]
            [assembly: AssemblyMetadata("DRN.Conventions.Role.Domain", "Sample.Domain")]
            [assembly: AssemblyMetadata("DRN.Conventions.{{key}}", "{{value}}")]
            namespace Sample.Domain.QA;
            public class Category;
            """).WithAssemblyName("Sample.Domain");
        var consumer = Compilation("public class Consumer;").AddReferences(Reference(producer, true));
        var facts = Facts(consumer);
        facts.Owner(consumer.GetTypeByMetadataName("Sample.Domain.QA.Category")!).Should().BeNull();
        facts.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCONVENTIONINPUT001");
    }

    [Theory]
    [InlineData("")]
    [InlineData("[assembly: System.Reflection.AssemblyMetadata(\"DRN.Conventions.ProjectName\", null)]")]
    public void Missing_Producer_Name_Should_Use_Assembly_Identity_But_Explicit_Null_Should_Be_Rejected(string projectName)
    {
        var producer = Compilation(projectName + """
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.RootNamespace", "Sample.Domain")]
            namespace Sample.Domain.QA;
            public class Category;
            """).WithAssemblyName("Sample.Domain");
        var consumer = Compilation("public class Consumer;").AddReferences(Reference(producer, true));
        var facts = Facts(consumer);
        var type = consumer.GetTypeByMetadataName("Sample.Domain.QA.Category")!;
        if (projectName.Length == 0)
        {
            facts.Owner(type)!.Subdomain.Should().Be("QA");
            facts.Diagnostics.Should().BeEmpty();
            return;
        }
        facts.Owner(type).Should().BeNull();
        facts.Owner(type).Should().BeNull();
        var diagnostic = facts.Diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNCONVENTIONINPUT001");
        diagnostic.GetMessage().Should().Contain("reference-visible project identity");
    }

    [Fact]
    public void Assembly_Metadata_Lookalike_Should_Not_Export_Project_Identity()
    {
        var producer = Compilation("""
            [assembly: Local.AssemblyMetadata("DRN.Conventions.ProjectName", "Sample.Domain")]
            [assembly: Local.AssemblyMetadata("DRN.Conventions.RootNamespace", "Sample.Domain")]
            namespace Local
            {
                [System.AttributeUsage(System.AttributeTargets.Assembly, AllowMultiple = true)]
                public class AssemblyMetadataAttribute(string key, string value) : System.Attribute;
            }
            namespace Sample.Domain.QA { public class Category; }
            """).WithAssemblyName("External.Library");
        var consumer = Compilation("public class Consumer;").AddReferences(Reference(producer, true));
        var facts = Facts(consumer);
        facts.Owner(consumer.GetTypeByMetadataName("Sample.Domain.QA.Category")!).Should().BeNull();
        facts.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Repeated_Malformed_Producer_Key_Should_Report_Once_Without_Invalidating_Independent_Ownership()
    {
        var producer = Compilation("""
            using System.Reflection;
            [assembly: AssemblyMetadata("DRN.Conventions.ProjectName", "Sample.Domain")]
            [assembly: AssemblyMetadata("DRN.Conventions.RootNamespace", "Sample.Domain")]
            [assembly: AssemblyMetadata("DRN.Conventions.Ownership.Broken", "QA")]
            [assembly: AssemblyMetadata("DRN.Conventions.Ownership.Broken", "QA")]
            namespace Sample.Domain.QA;
            public class Category;
            """).WithAssemblyName("Sample.Domain");
        var consumer = Compilation("public class Consumer;").AddReferences(Reference(producer, true));
        var facts = Facts(consumer);
        var category = consumer.GetTypeByMetadataName("Sample.Domain.QA.Category")!;
        facts.Owner(category)!.Subdomain.Should().Be("QA");
        facts.Owner(category)!.Subdomain.Should().Be("QA");
        var diagnostic = facts.Diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNCONVENTIONINPUT001");
        diagnostic.GetMessage().Should().Contain("Ownership.Broken").And.Contain("malformed");
    }

    [Theory]
    [InlineData("QA", "", false)]
    [InlineData("Other", "", true)]
    [InlineData("Other", "External technical owner is explicitly mapped.", false)]
    public void Referenced_Ownership_Should_Require_Reason_Only_For_An_Exception(string owner, string reason, bool invalid)
    {
        var producer = Compilation($$"""
            using System.Reflection;
            using DRN.Framework.SharedKernel.Attributes;
            [assembly: AssemblyMetadata("DRN.Conventions.ProjectName", "Sample.Domain")]
            [assembly: AssemblyMetadata("DRN.Conventions.RootNamespace", "Sample.Domain")]
            namespace Sample.Domain.QA;
            [SourceKnownSubdomain({{SymbolDisplay.FormatLiteral(owner, true)}}, Reason = {{SymbolDisplay.FormatLiteral(reason, true)}})]
            public class Category;
            """).WithAssemblyName("Sample.Domain");
        producer.GetDiagnostics().Should().NotContain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        using var stream = new MemoryStream();
        var emission = producer.Emit(stream, options: new EmitOptions(metadataOnly: true, includePrivateMembers: false));
        emission.Success.Should().BeTrue(string.Join(Environment.NewLine, emission.Diagnostics));
        var consumer = Compilation("namespace Sample.Infra.QA; public class Consumer;").WithAssemblyName("Sample.Infra")
            .AddReferences(MetadataReference.CreateFromImage(stream.ToArray()));
        var facts = Facts(consumer);
        var category = consumer.GetTypeByMetadataName("Sample.Domain.QA.Category")!;
        var result = facts.Owner(category);
        facts.HasInvalidOwner(category).Should().Be(invalid);
        if (invalid)
        {
            result.Should().BeNull();
            facts.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCONVENTIONINPUT001");
        }
        else
        {
            result!.Subdomain.Should().Be(owner);
            result.AssemblyName.Should().Be("Sample.Domain");
            facts.Diagnostics.Should().BeEmpty();
        }
    }

    [Fact]
    public void Logical_Path_Overlap_Should_Not_Be_Hidden_By_An_Intervening_Punctuation_Prefix()
    {
        var compilation = Compilation("""
            namespace Sample.Split.First { public class First; }
            namespace Sample.Split.Middle { public class Middle; }
            namespace Sample.Split.Child { public class Child; }
            """);
        var facts = Facts(compilation,
            Role("Domain", "Sample.Split.First", "A"),
            Role("Contract", "Sample.Split.Middle", "A-"),
            Role("Infra", "Sample.Split.Child", "A/B"));
        facts.Role(compilation.GetTypeByMetadataName("Sample.Split.First.First")!).Role.Should().BeEmpty();
        facts.Role(compilation.GetTypeByMetadataName("Sample.Split.Child.Child")!).Role.Should().BeEmpty();
        facts.Role(compilation.GetTypeByMetadataName("Sample.Split.Middle.Middle")!).Role.Should().Be("Contract");
        facts.Diagnostics.Should().HaveCount(2).And.OnlyContain(diagnostic => diagnostic.Id == "DRNCONVENTIONINPUT001");
        facts.Diagnostics.Select(diagnostic => diagnostic.GetMessage()).Should().BeEquivalentTo(
            "Convention input 'Role:Domain' is overlapping another role mapping. Declare one valid set of nonoverlapping namespace and logical-path prefixes.",
            "Convention input 'Role:Infra' is overlapping another role mapping. Declare one valid set of nonoverlapping namespace and logical-path prefixes.");
    }

    [Fact]
    public void Invalid_Logical_Input_Should_Not_Expose_Physical_Paths_In_The_Message()
    {
        var location = Location.Create("DrnConventionInputs.g.cs", new(0, 1), new(new(0, 0), new(0, 1)));
        var facts = Facts(Compilation("public class Consumer;"), new ConventionInput("Path", "/private/producer.cs",
            ImmutableDictionary<string, string>.Empty.Add("LogicalPath", "/private/invalid.cs"), location));
        var diagnostic = facts.Diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNCONVENTIONINPUT001");
        diagnostic.GetMessage().Should().Contain("Path:Compile/Link").And.NotContain("/private/");
        diagnostic.GetMessage().Should().Be("Convention input 'Path:Compile/Link' is declared with an invalid logical path. Declare one valid project-relative Compile or Link path.");
        diagnostic.Location.Should().Be(location);
    }

    [Fact]
    public void Unknown_Input_Fields_Should_Have_A_Grammatical_Diagnostic()
    {
        var facts = Facts(Compilation("public class Consumer;"), new ConventionInput("Context", "Consumer",
            ImmutableDictionary<string, string>.Empty.Add("Unsupported", "value"), Location.None));
        facts.Diagnostics.Should().ContainSingle().Which.GetMessage().Should().Be(
            "Convention input 'Context:Consumer' is declared with unknown fields. Declare one valid record using documented fields for Context.");
    }

    [Fact]
    public void Referenced_Source_Attributes_Should_Not_Report_Foreign_Syntax_Locations()
    {
        var producer = Compilation("""
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.ProjectName", "Sample.Domain")]
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.RootNamespace", "Sample.Domain")]
            namespace Sample.Domain.QA;
            [DRN.Framework.SharedKernel.Attributes.SourceKnownSubdomain("Other")]
            public class Category;
            """).WithAssemblyName("Sample.Domain");
        var consumer = Compilation("public class Consumer;").WithAssemblyName("Sample.Infra")
            .AddReferences(producer.ToMetadataReference());
        var facts = Facts(consumer);
        var category = consumer.GetTypeByMetadataName("Sample.Domain.QA.Category")!;
        facts.Owner(category).Should().BeNull();
        facts.Location(category).Should().Be(Location.None);
        facts.Diagnostics.Should().ContainSingle().Which.Location.Should().Be(Location.None);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Producer_Project_Ownership_And_Classification_Should_Survive_Source_And_Emitted_References(bool emitted)
    {
        var producer = Compilation("""
            using System.Reflection;
            [assembly: AssemblyMetadata("DRN.Conventions.ProjectName", "Sample.Domain")]
            [assembly: AssemblyMetadata("DRN.Conventions.RootNamespace", "Sample.Domain")]
            [assembly: AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.Legacy.Account.Subdomain", "Identity")]
            [assembly: AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.Legacy.Account.Reason", "Preserve the published account namespace.")]
            [assembly: AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.Legacy.Account.Kind", "Entity")]
            [assembly: AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.Shared.Money.Subdomain", "Shared")]
            [assembly: AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.Shared.Money.Reason", "")]
            [assembly: AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.Shared.Money.Kind", "Value")]
            namespace Sample.Domain.Legacy { public class Account; }
            namespace Sample.Domain.Shared { public class Money; }
            """).WithAssemblyName("Sample.Domain");
        var consumer = Compilation("public class Consumer;").WithAssemblyName("Sample.Infra").AddReferences(Reference(producer, emitted));
        var facts = Facts(consumer);
        var account = consumer.GetTypeByMetadataName("Sample.Domain.Legacy.Account")!;
        var money = consumer.GetTypeByMetadataName("Sample.Domain.Shared.Money")!;
        facts.IsEntity(account).Should().BeTrue();
        facts.Owner(account)!.Subdomain.Should().Be("Identity");
        facts.Owner(account)!.AssemblyName.Should().Be("Sample.Domain");
        facts.IsNeutralValue(money).Should().BeTrue();
        facts.IsEntity(money).Should().BeFalse();
        facts.Diagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Producer_Context_Owner_Should_Survive_Reference_Boundaries(bool emitted)
    {
        var framework = AnalyzerTestCompilation.EntityFrameworkReference;
        var utils = AnalyzerTestCompilation.UtilsReference;
        var producer = Compilation("""
            using System.Reflection;
            [assembly: AssemblyMetadata("DRN.Conventions.ProjectName", "Sample.Infra")]
            [assembly: AssemblyMetadata("DRN.Conventions.RootNamespace", "Sample.Infra")]
            [assembly: AssemblyMetadata("DRN.Conventions.Context.Sample.Infra.Legacy.AccountContext.Subdomain", "Identity")]
            [assembly: AssemblyMetadata("DRN.Conventions.Context.Sample.Infra.Legacy.AccountContext.Reason", "Preserve storage and context identity.")]
            namespace Sample.Infra.Legacy;
            public class AccountContext() : DRN.Framework.EntityFramework.Context.DrnContext<AccountContext>(null);
            """).WithAssemblyName("Sample.Infra").AddReferences(framework, utils);
        var consumer = Compilation("public class Consumer;").WithAssemblyName("Sample.Hosted")
            .AddReferences(framework, utils, Reference(producer, emitted));
        var facts = Facts(consumer);
        var context = consumer.GetTypeByMetadataName("Sample.Infra.Legacy.AccountContext")!;
        facts.Owner(context)!.Subdomain.Should().Be("Identity");
        facts.Owner(context)!.Role.Should().Be("Infra");
        facts.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void A_Consumer_Context_Item_Should_Not_Redefine_A_Referenced_Conventional_Owner()
    {
        var framework = AnalyzerTestCompilation.EntityFrameworkReference;
        var utils = AnalyzerTestCompilation.UtilsReference;
        var producer = Compilation("""
            using System.Reflection;
            [assembly: AssemblyMetadata("DRN.Conventions.ProjectName", "Sample.Infra")]
            [assembly: AssemblyMetadata("DRN.Conventions.RootNamespace", "Sample.Infra")]
            namespace Sample.Infra.QA;
            public class QAContext() : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>(null);
            """).WithAssemblyName("Sample.Infra").AddReferences(framework, utils);
        var consumer = Compilation("public class Consumer;").WithAssemblyName("Sample.Hosted")
            .AddReferences(framework, utils, Reference(producer, true));
        var input = new ConventionInput("Context", "Sample.Infra.QA.QAContext",
            ImmutableDictionary<string, string>.Empty.Add("Subdomain", "Billing").Add("Reason", "Attempted consumer context alias."), Location.None);
        var facts = Facts(consumer, input);
        facts.Owner(consumer.GetTypeByMetadataName("Sample.Infra.QA.QAContext")!).Should().BeNull();
        facts.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCONVENTIONINPUT001");
    }

    [Theory]
    [InlineData("Subdomain", "Other")]
    [InlineData("Kind", "Value")]
    public void Contradictory_Producer_Metadata_Should_Be_Reported_Once_And_Not_Used(string field, string value)
    {
        var producer = Compilation($$"""
            using System.Reflection;
            [assembly: AssemblyMetadata("DRN.Conventions.ProjectName", "Sample.Domain")]
            [assembly: AssemblyMetadata("DRN.Conventions.RootNamespace", "Sample.Domain")]
            [assembly: AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.QA.User.Subdomain", "QA")]
            [assembly: AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.QA.User.Kind", "Entity")]
            [assembly: AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.QA.User.{{field}}", "{{value}}")]
            namespace Sample.Domain.QA;
            public class User;
            """).WithAssemblyName("Sample.Domain");
        var consumer = Compilation("public class Consumer;").WithAssemblyName("Sample.Infra").AddReferences(Reference(producer, false));
        var facts = Facts(consumer);
        var user = consumer.GetTypeByMetadataName("Sample.Domain.QA.User")!;
        facts.IsEntity(user).Should().BeFalse();
        facts.Owner(user).Should().BeNull();
        facts.IsNeutralValue(user).Should().BeFalse();
        facts.Owner(user).Should().BeNull();
        facts.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCONVENTIONINPUT001");
    }

    [Theory]
    [InlineData("Identity", "Entity", false)]
    [InlineData("Billing", "Entity", true)]
    [InlineData("Identity", "Value", true)]
    public void Consumer_Inputs_Should_Not_Redefine_The_Producer_Owner_Or_Entity_Kind(string owner, string kind, bool invalid)
    {
        var producer = Compilation("""
            using System.Reflection;
            [assembly: AssemblyMetadata("DRN.Conventions.ProjectName", "Sample.Domain")]
            [assembly: AssemblyMetadata("DRN.Conventions.RootNamespace", "Sample.Domain")]
            [assembly: AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.Legacy.Account.Subdomain", "Identity")]
            [assembly: AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.Legacy.Account.Reason", "Compatibility namespace.")]
            [assembly: AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.Legacy.Account.Kind", "Entity")]
            namespace Sample.Domain.Legacy;
            public class Account;
            """).WithAssemblyName("Sample.Domain");
        var consumer = Compilation("public class Consumer;").WithAssemblyName("Sample.Infra").AddReferences(Reference(producer, true));
        var input = new ConventionInput("Ownership", "Sample.Domain.Legacy.Account",
            ImmutableDictionary<string, string>.Empty.Add("Subdomain", owner).Add("Kind", kind).Add("Reason", "Explicit local declaration."), Location.None);
        var facts = Facts(consumer, input);
        var account = consumer.GetTypeByMetadataName("Sample.Domain.Legacy.Account")!;
        var resolved = facts.Owner(account);
        if (invalid)
        {
            resolved.Should().BeNull();
            facts.IsNeutralValue(account).Should().BeFalse();
            facts.Diagnostics.Should().NotBeEmpty().And.OnlyContain(diagnostic => diagnostic.Id == "DRNCONVENTIONINPUT001");
        }
        else
        {
            resolved!.Subdomain.Should().Be("Identity");
            facts.IsEntity(account).Should().BeTrue();
            facts.Diagnostics.Should().BeEmpty();
        }
    }

    [Fact]
    public void An_Unclassified_Assembly_Should_Not_Acquire_The_Consumers_Application_Root()
    {
        var producer = Compilation("""
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Ownership.External.User.Subdomain", "Identity")]
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Ownership.External.User.Kind", "Entity")]
            namespace External;
            public class User;
            """).WithAssemblyName("External");
        var consumer = Compilation("public class Consumer;").WithAssemblyName("Sample.Infra").AddReferences(Reference(producer, true));
        var facts = Facts(consumer);
        var user = consumer.GetTypeByMetadataName("External.User")!;
        facts.IsEntity(user).Should().BeFalse();
        facts.Owner(user).Should().BeNull();
        facts.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Another_Assembly_Should_Not_Export_A_New_Owner_For_A_Referenced_Type()
    {
        var producer = Compilation("""
            using System.Reflection;
            [assembly: AssemblyMetadata("DRN.Conventions.ProjectName", "Sample.Domain")]
            [assembly: AssemblyMetadata("DRN.Conventions.RootNamespace", "Sample.Domain")]
            [assembly: AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.QA.User.Subdomain", "QA")]
            [assembly: AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.QA.User.Kind", "Entity")]
            namespace Sample.Domain.QA;
            public class User;
            """).WithAssemblyName("Sample.Domain");
        var ownerReference = Reference(producer, true);
        var other = Compilation("""
            using System.Reflection;
            [assembly: AssemblyMetadata("DRN.Conventions.ProjectName", "Sample.Infra")]
            [assembly: AssemblyMetadata("DRN.Conventions.RootNamespace", "Sample.Infra")]
            [assembly: AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.QA.User.Subdomain", "Billing")]
            [assembly: AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.QA.User.Reason", "Attempted imported-owner replacement.")]
            [assembly: AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.QA.User.Kind", "Value")]
            public class Marker;
            """).WithAssemblyName("Sample.Infra").AddReferences(ownerReference);
        var consumer = Compilation("public class Consumer;").WithAssemblyName("Sample.Hosted")
            .AddReferences(ownerReference, Reference(other, true));
        var facts = Facts(consumer);
        var user = consumer.GetTypeByMetadataName("Sample.Domain.QA.User")!;
        facts.IsEntity(user).Should().BeTrue();
        facts.IsNeutralValue(user).Should().BeFalse();
        facts.Owner(user)!.Subdomain.Should().Be("QA");
        facts.Diagnostics.Should().BeEmpty();
    }

    private static MetadataReference Reference(CSharpCompilation producer, bool emitted)
    {
        producer.GetDiagnostics().Should().NotContain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        if (!emitted) return producer.ToMetadataReference();
        using var stream = new MemoryStream();
        var result = producer.Emit(stream, options: new EmitOptions(metadataOnly: true, includePrivateMembers: false));
        result.Success.Should().BeTrue(string.Join(Environment.NewLine, result.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    private static ConventionInput Role(string role, string ns, string path) => new("Role", role,
        ImmutableDictionary<string, string>.Empty.Add("NamespacePrefix", ns).Add("LogicalPathPrefix", path), Location.None);

    private static ConventionFacts Facts(CSharpCompilation compilation, params ConventionInput[] inputs) =>
        new(compilation, new(true, "Sample.Infra", "Infra", "Sample.Infra", compilation.AssemblyName!, []),
            new([.. inputs], []), new Options(), CancellationToken.None);

    private sealed class Options(string generated = "") : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new ConventionInputTests.Config(new());
        private readonly AnalyzerConfigOptions _treeOptions = new ConventionInputTests.Config(new() { ["generated_code"] = generated });
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _treeOptions;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;
    }
}
