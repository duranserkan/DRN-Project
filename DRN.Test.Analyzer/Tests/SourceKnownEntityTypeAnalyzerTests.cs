namespace DRN.Test.Analyzer.Tests;

public class SourceKnownEntityTypeAnalyzerTests
{
    private const string SharedKernelDomainStubs = """
        namespace DRN.Framework.SharedKernel.Domain
        {
            using System;

            [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
            public sealed class EntityTypeAttribute(byte entityType) : Attribute
            {
                public byte EntityType { get; } = entityType;
            }

            public abstract class SourceKnownEntity(long id = 0)
            {
                public long Id { get; set; } = id;
            }

            public abstract class AggregateRoot(long id = 0) : SourceKnownEntity(id);
        }
        """;

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(params string[] sources)
    {
        var syntaxTrees = sources
            .Select((s, i) => CSharpSyntaxTree.ParseText(s, path: $"Source{i}.cs"))
            .Concat([CSharpSyntaxTree.ParseText(SharedKernelDomainStubs, path: "SharedKernelDomainStubs.cs")])
            .ToArray();

        var coreAssemblyPath = typeof(object).Assembly.Location;
        var runtimeAssemblyPath = Path.Combine(Path.GetDirectoryName(coreAssemblyPath)!, "System.Runtime.dll");

        var references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(coreAssemblyPath),
            MetadataReference.CreateFromFile(runtimeAssemblyPath),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location)
        };

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compilerErrors = compilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();

        if (!compilerErrors.IsEmpty)
        {
            var errorMessages = string.Join(Environment.NewLine, compilerErrors.Select(d => d.ToString()));
            throw new InvalidOperationException($"Test compilation failed with compiler errors:{Environment.NewLine}{errorMessages}");
        }

        var analyzer = new SourceKnownEntityTypeAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ValidEntity_WithEntityType_ProducesNoDiagnostics()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            [EntityType(1)]
            public class ValidEntity : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task MissingEntityType_OnDirectSourceKnownEntityDescendant_ProducesDRN0001()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public class MissingAttributeEntity : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DRN0001");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("MissingAttributeEntity");
    }

    [Fact]
    public async Task MissingEntityType_OnIndirectDescendant_ProducesDRN0001()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public abstract class BaseDomainEntity : AggregateRoot;

            public class ChildEntity : BaseDomainEntity;
            """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DRN0001");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("ChildEntity");
    }

    [Fact]
    public async Task AbstractClass_WithoutEntityType_ProducesNoDiagnostics()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public abstract class AbstractDomainEntity : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DuplicateEntityType_AcrossTwoEntities_ProducesDRN0002()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            [EntityType(5)]
            public class FirstEntity : SourceKnownEntity;

            [EntityType(5)]
            public class SecondEntity : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DRN0002");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("5");
    }

    [Fact]
    public async Task DistinctEntityTypes_AcrossMultipleEntities_ProducesNoDiagnostics()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            [EntityType(1)]
            public class FirstEntity : SourceKnownEntity;

            [EntityType(2)]
            public class SecondEntity : SourceKnownEntity;

            [EntityType(3)]
            public class ThirdEntity : AggregateRoot;
            """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task InvalidUsage_EntityTypeOnAbstractClass_ProducesDRN0003()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            [EntityType(1)]
            public abstract class AbstractEntityWithAttribute : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DRN0003");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("AbstractEntityWithAttribute");
    }

    [Fact]
    public async Task InvalidUsage_EntityTypeOnNonSourceKnownEntity_ProducesDRN0003()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            [EntityType(1)]
            public class RegularNonEntityClass;
            """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DRN0003");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("RegularNonEntityClass");
    }

    [Fact]
    public async Task EnumCastInEntityType_WorksAndDetectsDuplicates()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public enum DomainEntityTypes : byte
            {
                ItemOne = 10,
                ItemDuplicate = 10
            }

            [EntityType((byte)DomainEntityTypes.ItemOne)]
            public class EntityA : SourceKnownEntity;

            [EntityType((byte)DomainEntityTypes.ItemDuplicate)]
            public class EntityB : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DRN0002");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("10");
    }

    [Fact]
    public async Task DuplicateEntityName_AcrossDifferentNamespaces_ProducesDRN0004()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            namespace DomainA
            {
                [EntityType(1)]
                public class User : SourceKnownEntity;
            }

            namespace DomainB
            {
                [EntityType(2)]
                public class User : SourceKnownEntity;
            }
            """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DRN0004");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostics[0].GetMessage().Should().Contain("User");
    }

    [Fact]
    public async Task PrivateNestedEntity_DoesNotProduceDRN0004()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            namespace DomainA
            {
                [EntityType(1)]
                public class User : SourceKnownEntity;
            }

            namespace Tests
            {
                public class TestFixture
                {
                    [EntityType(2)]
                    private class User : SourceKnownEntity;
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DuplicateEntityType_AcrossReferencedAssembly_ProducesDRN0002()
    {
        const string referencedSource = """
            using DRN.Framework.SharedKernel.Domain;

            namespace ExternalDomain
            {
                [EntityType(42)]
                public class ExternalEntity : SourceKnownEntity;
            }
            """;

        var (referencedMetadata, references) = CreateReferencedAssemblyMetadata(referencedSource);

        const string consumingSource = """
            using DRN.Framework.SharedKernel.Domain;

            namespace LocalDomain
            {
                [EntityType(42)]
                public class LocalEntity : SourceKnownEntity;
            }
            """;

        var consumingTree = CSharpSyntaxTree.ParseText(consumingSource + "\n" + SharedKernelDomainStubs);
        var consumingCompilation = CSharpCompilation.Create(
            "ConsumingAssembly",
            [consumingTree],
            references.Append(referencedMetadata),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzer = new SourceKnownEntityTypeAnalyzer();
        var compilationWithAnalyzers = consumingCompilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(CancellationToken.None);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DRN0002");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("42");
        diagnostics[0].GetMessage().Should().Contain("ExternalEntity");
    }

    [Fact]
    public async Task DuplicateEntityName_AcrossReferencedAssembly_ProducesDRN0004()
    {
        const string referencedSource = """
            using DRN.Framework.SharedKernel.Domain;

            namespace ExternalDomain
            {
                [EntityType(1)]
                public class Order : SourceKnownEntity;
            }
            """;

        var (referencedMetadata, references) = CreateReferencedAssemblyMetadata(referencedSource);

        const string consumingSource = """
            using DRN.Framework.SharedKernel.Domain;

            namespace LocalDomain
            {
                [EntityType(2)]
                public class Order : SourceKnownEntity;
            }
            """;

        var consumingTree = CSharpSyntaxTree.ParseText(consumingSource + "\n" + SharedKernelDomainStubs);
        var consumingCompilation = CSharpCompilation.Create(
            "ConsumingAssembly",
            [consumingTree],
            references.Append(referencedMetadata),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzer = new SourceKnownEntityTypeAnalyzer();
        var compilationWithAnalyzers = consumingCompilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(CancellationToken.None);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DRN0004");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostics[0].GetMessage().Should().Contain("Order");
    }

    [Fact]
    public async Task PrivateNestedEntity_DoesNotCollideWithReferencedAssembly()
    {
        const string referencedSource = """
            using DRN.Framework.SharedKernel.Domain;

            namespace ExternalDomain
            {
                [EntityType(42)]
                public class ExternalEntity : SourceKnownEntity;
            }
            """;

        var (referencedMetadata, references) = CreateReferencedAssemblyMetadata(referencedSource);

        const string consumingSource = """
            using DRN.Framework.SharedKernel.Domain;

            namespace LocalTests
            {
                public class Fixture
                {
                    [EntityType(42)]
                    private class ExternalEntity : SourceKnownEntity;
                }
            }
            """;

        var consumingTree = CSharpSyntaxTree.ParseText(consumingSource + "\n" + SharedKernelDomainStubs);
        var consumingCompilation = CSharpCompilation.Create(
            "ConsumingAssembly",
            [consumingTree],
            references.Append(referencedMetadata),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzer = new SourceKnownEntityTypeAnalyzer();
        var compilationWithAnalyzers = consumingCompilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(CancellationToken.None);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void DiagnosticDescriptors_AllHaveHelpLinkUri()
    {
        DiagnosticDescriptors.MissingEntityTypeAttribute.HelpLinkUri.Should().Be(DiagnosticDescriptors.HelpLinkUri);
        DiagnosticDescriptors.DuplicateEntityTypeValue.HelpLinkUri.Should().Be(DiagnosticDescriptors.HelpLinkUri);
        DiagnosticDescriptors.DuplicateEntityTypeValue.CustomTags.Should().Contain(WellKnownDiagnosticTags.CompilationEnd);
        DiagnosticDescriptors.InvalidEntityTypeAttributeUsage.HelpLinkUri.Should().Be(DiagnosticDescriptors.HelpLinkUri);
        DiagnosticDescriptors.DuplicateEntityName.HelpLinkUri.Should().Be(DiagnosticDescriptors.HelpLinkUri);
    }

    [Fact]
    public async Task DuplicateEntityType_MultipleEntities_ReportsDeterministicallyOnSubsequentDeclarations()
    {
        const string source1 = """
            using DRN.Framework.SharedKernel.Domain;

            [EntityType(7)]
            public class AlphaEntity : SourceKnownEntity;
            """;

        const string source2 = """
            using DRN.Framework.SharedKernel.Domain;

            [EntityType(7)]
            public class BetaEntity : SourceKnownEntity;

            [EntityType(7)]
            public class GammaEntity : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerAsync(source1, source2);

        diagnostics.Should().HaveCount(2);
        diagnostics.Should().OnlyContain(d => d.Id == "DRN0002" && d.Severity == DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("BetaEntity").And.Contain("AlphaEntity");
        diagnostics[1].GetMessage().Should().Contain("GammaEntity").And.Contain("AlphaEntity");
    }

    private static (MetadataReference Metadata, MetadataReference[] BaseReferences) CreateReferencedAssemblyMetadata(string source)
    {
        var refSyntaxTree = CSharpSyntaxTree.ParseText(source + "\n" + SharedKernelDomainStubs);
        var coreAssemblyPath = typeof(object).Assembly.Location;
        var runtimeAssemblyPath = Path.Combine(Path.GetDirectoryName(coreAssemblyPath)!, "System.Runtime.dll");

        var references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(coreAssemblyPath),
            MetadataReference.CreateFromFile(runtimeAssemblyPath),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location)
        };

        var refCompilation = CSharpCompilation.Create(
            "ReferencedDomainAssembly",
            [refSyntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var emitResult = refCompilation.Emit(stream);
        emitResult.Success.Should().BeTrue();
        stream.Seek(0, SeekOrigin.Begin);

        var metadata = MetadataReference.CreateFromImage(stream.ToArray());
        return (metadata, references);
    }
}
