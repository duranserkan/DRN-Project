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
            .Concat([SharedKernelDomainStubs])
            .Select(s => CSharpSyntaxTree.ParseText(s))
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
}
