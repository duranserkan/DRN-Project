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

    private static readonly Lazy<MetadataReference> SharedKernelReference = new(() =>
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(SharedKernelDomainStubs, path: "SharedKernelDomainStubs.cs");
        var coreAssemblyPath = typeof(object).Assembly.Location;
        var runtimeAssemblyPath = Path.Combine(Path.GetDirectoryName(coreAssemblyPath)!, "System.Runtime.dll");

        var references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(coreAssemblyPath),
            MetadataReference.CreateFromFile(runtimeAssemblyPath),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location)
        };

        var compilation = CSharpCompilation.Create(
            "DRN.Framework.SharedKernel",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);
        if (!emitResult.Success)
        {
            var errors = string.Join(Environment.NewLine, emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            throw new InvalidOperationException($"SharedKernel stub compilation failed:{Environment.NewLine}{errors}");
        }

        stream.Seek(0, SeekOrigin.Begin);
        return MetadataReference.CreateFromImage(stream.ToArray());
    });

    private static MetadataReference[] GetBaseReferences()
    {
        var coreAssemblyPath = typeof(object).Assembly.Location;
        var runtimeAssemblyPath = Path.Combine(Path.GetDirectoryName(coreAssemblyPath)!, "System.Runtime.dll");

        return
        [
            MetadataReference.CreateFromFile(coreAssemblyPath),
            MetadataReference.CreateFromFile(runtimeAssemblyPath),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            SharedKernelReference.Value
        ];
    }

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(params string[] sources)
    {
        var syntaxTrees = sources
            .Select((s, i) => CSharpSyntaxTree.ParseText(s, path: $"Source{i}.cs"))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            GetBaseReferences(),
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

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerWithReferenceAsync(string referencedSource, string consumingSource)
    {
        var refSyntaxTree = CSharpSyntaxTree.ParseText(referencedSource, path: "ReferencedSource.cs");
        var baseReferences = GetBaseReferences();

        var refCompilation = CSharpCompilation.Create(
            "ReferencedDomainAssembly",
            [refSyntaxTree],
            baseReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var emitResult = refCompilation.Emit(stream);
        if (!emitResult.Success)
        {
            var errors = string.Join(Environment.NewLine, emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            throw new InvalidOperationException($"Referenced assembly compilation failed:{Environment.NewLine}{errors}");
        }

        stream.Seek(0, SeekOrigin.Begin);
        var referencedMetadata = MetadataReference.CreateFromImage(stream.ToArray());

        var consumingTree = CSharpSyntaxTree.ParseText(consumingSource, path: "ConsumingSource.cs");
        var consumingCompilation = CSharpCompilation.Create(
            "ConsumingAssembly",
            [consumingTree],
            baseReferences.Append(referencedMetadata),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compilerErrors = consumingCompilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();

        if (!compilerErrors.IsEmpty)
        {
            var errorMessages = string.Join(Environment.NewLine, compilerErrors.Select(d => d.ToString()));
            throw new InvalidOperationException($"Consuming compilation failed with compiler errors:{Environment.NewLine}{errorMessages}");
        }

        var analyzer = new SourceKnownEntityTypeAnalyzer();
        var compilationWithAnalyzers = consumingCompilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
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
    public async Task InvalidUsage_EntityTypeOnPrivateClass_ProducesDRN0003()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public class Fixture
            {
                [EntityType(1)]
                private class PrivateEntity : SourceKnownEntity;
            }
            """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DRN0003");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("PrivateEntity");
    }

    [Fact]
    public async Task PrivateClass_WithoutEntityType_DoesNotProduceDiagnostics()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public class Fixture
            {
                private class PrivateEntity : SourceKnownEntity;
            }
            """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().BeEmpty();
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

        const string consumingSource = """
            using DRN.Framework.SharedKernel.Domain;

            namespace LocalDomain
            {
                [EntityType(42)]
                public class LocalEntity : SourceKnownEntity;
            }
            """;

        var diagnostics = await RunAnalyzerWithReferenceAsync(referencedSource, consumingSource);

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

        const string consumingSource = """
            using DRN.Framework.SharedKernel.Domain;

            namespace LocalDomain
            {
                [EntityType(2)]
                public class Order : SourceKnownEntity;
            }
            """;

        var diagnostics = await RunAnalyzerWithReferenceAsync(referencedSource, consumingSource);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DRN0004");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostics[0].GetMessage().Should().Contain("Order");
    }

    [Fact]
    public async Task PrivateNestedEntity_WithoutAttribute_DoesNotCollideWithReferencedAssembly()
    {
        const string referencedSource = """
            using DRN.Framework.SharedKernel.Domain;

            namespace ExternalDomain
            {
                [EntityType(42)]
                public class ExternalEntity : SourceKnownEntity;
            }
            """;

        const string consumingSource = """
            using DRN.Framework.SharedKernel.Domain;

            namespace LocalTests
            {
                public class Fixture
                {
                    private class ExternalEntity : SourceKnownEntity;
                }
            }
            """;

        var diagnostics = await RunAnalyzerWithReferenceAsync(referencedSource, consumingSource);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task PublicConsumingEntity_DoesNotCollideWithPrivateNestedEntityInReferencedAssembly()
    {
        const string referencedSource = """
            using DRN.Framework.SharedKernel.Domain;

            namespace ExternalDomain
            {
                public class ExternalFixture
                {
                    private class ConflictingEntity : SourceKnownEntity;
                }
            }
            """;

        const string consumingSource = """
            using DRN.Framework.SharedKernel.Domain;

            namespace LocalDomain
            {
                [EntityType(42)]
                public class ConflictingEntity : SourceKnownEntity;
            }
            """;

        var diagnostics = await RunAnalyzerWithReferenceAsync(referencedSource, consumingSource);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task PublicConsumingEntity_DoesNotProduceDRN0004_ForPrivateNestedEntityInReferencedAssembly()
    {
        const string referencedSource = """
            using DRN.Framework.SharedKernel.Domain;

            namespace ExternalDomain
            {
                public class ExternalFixture
                {
                    private class ConflictingEntity : SourceKnownEntity;
                }
            }
            """;

        const string consumingSource = """
            using DRN.Framework.SharedKernel.Domain;

            namespace LocalDomain
            {
                [EntityType(2)]
                public class ConflictingEntity : SourceKnownEntity;
            }
            """;

        var diagnostics = await RunAnalyzerWithReferenceAsync(referencedSource, consumingSource);

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
        DiagnosticDescriptors.DuplicateEntityName.CustomTags.Should().Contain(WellKnownDiagnosticTags.CompilationEnd);
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

        var rawDiagnostics = await RunAnalyzerAsync(source1, source2);
        var diagnostics = rawDiagnostics
            .OrderBy(d => d.Location.SourceTree?.FilePath, StringComparer.Ordinal)
            .ThenBy(d => d.Location.SourceSpan.Start)
            .ThenBy(d => d.Location.SourceSpan.Length)
            .ToImmutableArray();

        diagnostics.Should().HaveCount(2);
        diagnostics.Should().OnlyContain(d => d.Id == "DRN0002" && d.Severity == DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("BetaEntity").And.Contain("AlphaEntity");
        diagnostics[1].GetMessage().Should().Contain("GammaEntity").And.Contain("AlphaEntity");
    }

    [Fact]
    public async Task DuplicateEntityName_MultipleEntities_ReportsDeterministicallyOnSubsequentDeclarations()
    {
        const string source1 = """
            using DRN.Framework.SharedKernel.Domain;

            namespace DomainA
            {
                [EntityType(1)]
                public class Customer : SourceKnownEntity;
            }
            """;

        const string source2 = """
            using DRN.Framework.SharedKernel.Domain;

            namespace DomainB
            {
                [EntityType(2)]
                public class Customer : SourceKnownEntity;
            }

            namespace DomainC
            {
                [EntityType(3)]
                public class Customer : SourceKnownEntity;
            }
            """;

        var rawDiagnostics = await RunAnalyzerAsync(source1, source2);
        var diagnostics = rawDiagnostics
            .OrderBy(d => d.Location.SourceTree?.FilePath, StringComparer.Ordinal)
            .ThenBy(d => d.Location.SourceSpan.Start)
            .ThenBy(d => d.Location.SourceSpan.Length)
            .ToImmutableArray();

        diagnostics.Should().HaveCount(2);
        diagnostics.Should().OnlyContain(d => d.Id == "DRN0004" && d.Severity == DiagnosticSeverity.Warning);
        diagnostics[0].GetMessage().Should().Contain("DomainB.Customer").And.Contain("DomainA.Customer");
        diagnostics[1].GetMessage().Should().Contain("DomainC.Customer").And.Contain("DomainA.Customer");
    }

    [Fact]
    public async Task DuplicateEntityType_AcrossMultipleReferencedAssemblies_ProducesDRN0002AtCompilationEnd()
    {
        var referencedSources = new (string AssemblyName, string Source)[]
        {
            ("ReferencedAssemblyA", """
                using DRN.Framework.SharedKernel.Domain;

                namespace DomainA
                {
                    [EntityType(42)]
                    public class EntityA : SourceKnownEntity;
                }
                """),
            ("ReferencedAssemblyB", """
                using DRN.Framework.SharedKernel.Domain;

                namespace DomainB
                {
                    [EntityType(42)]
                    public class EntityB : SourceKnownEntity;
                }
                """)
        };

        var diagnostics = await RunAnalyzerWithMultipleReferencesAsync(referencedSources);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DRN0002");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].Location.Should().Be(Location.None);
        diagnostics[0].GetMessage().Should().Contain("42");
        diagnostics[0].GetMessage().Should().Contain("ReferencedAssemblyA::EntityA");
        diagnostics[0].GetMessage().Should().Contain("ReferencedAssemblyB::EntityB");
    }

    [Fact]
    public async Task DuplicateEntityName_AcrossMultipleReferencedAssemblies_ProducesDRN0004AtCompilationEnd()
    {
        var referencedSources = new (string AssemblyName, string Source)[]
        {
            ("ReferencedAssemblyA", """
                using DRN.Framework.SharedKernel.Domain;

                namespace DomainA
                {
                    [EntityType(1)]
                    public class Product : SourceKnownEntity;
                }
                """),
            ("ReferencedAssemblyB", """
                using DRN.Framework.SharedKernel.Domain;

                namespace DomainB
                {
                    [EntityType(2)]
                    public class Product : SourceKnownEntity;
                }
                """)
        };

        var diagnostics = await RunAnalyzerWithMultipleReferencesAsync(referencedSources);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DRN0004");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostics[0].Location.Should().Be(Location.None);
        diagnostics[0].GetMessage().Should().Contain("Product");
        diagnostics[0].GetMessage().Should().Contain("DomainA.Product");
        diagnostics[0].GetMessage().Should().Contain("DomainB.Product");
    }

    [Fact]
    public async Task DiamondReferencedAssembly_DoesNotProduceDuplicateDiagnostics()
    {
        var referencedSources = new (string AssemblyName, string Source)[]
        {
            ("CommonDomainAssembly", """
                using DRN.Framework.SharedKernel.Domain;

                namespace CommonDomain
                {
                    [EntityType(10)]
                    public class SharedEntity : SourceKnownEntity;
                }
                """),
            ("ReferencedAssemblyA", """
                using DRN.Framework.SharedKernel.Domain;
                using CommonDomain;

                namespace DomainA
                {
                    [EntityType(20)]
                    public class EntityA : SourceKnownEntity
                    {
                        public SharedEntity? Shared { get; set; }
                    }
                }
                """),
            ("ReferencedAssemblyB", """
                using DRN.Framework.SharedKernel.Domain;
                using CommonDomain;

                namespace DomainB
                {
                    [EntityType(30)]
                    public class EntityB : SourceKnownEntity
                    {
                        public SharedEntity? Shared { get; set; }
                    }
                }
                """)
        };

        var diagnostics = await RunAnalyzerWithMultipleReferencesAsync(referencedSources);

        diagnostics.Should().BeEmpty();
    }

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerWithMultipleReferencesAsync(
        (string AssemblyName, string Source)[] referencedSources,
        string consumingSource = "")
    {
        var baseReferences = GetBaseReferences();
        var additionalReferences = new List<MetadataReference>();

        foreach (var (assemblyName, source) in referencedSources)
        {
            var refSyntaxTree = CSharpSyntaxTree.ParseText(source, path: $"{assemblyName}.cs");
            var refCompilation = CSharpCompilation.Create(
                assemblyName,
                [refSyntaxTree],
                baseReferences.Concat(additionalReferences),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var stream = new MemoryStream();
            var emitResult = refCompilation.Emit(stream);
            if (!emitResult.Success)
            {
                var errors = string.Join(Environment.NewLine, emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
                throw new InvalidOperationException($"Referenced assembly '{assemblyName}' compilation failed:{Environment.NewLine}{errors}");
            }

            stream.Seek(0, SeekOrigin.Begin);
            additionalReferences.Add(MetadataReference.CreateFromImage(stream.ToArray()));
        }

        var consumingTrees = string.IsNullOrWhiteSpace(consumingSource)
            ? []
            : new[] { CSharpSyntaxTree.ParseText(consumingSource, path: "ConsumingSource.cs") };

        var consumingCompilation = CSharpCompilation.Create(
            "ConsumingAssembly",
            consumingTrees,
            baseReferences.Concat(additionalReferences),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compilerErrors = consumingCompilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();

        if (!compilerErrors.IsEmpty)
        {
            var errorMessages = string.Join(Environment.NewLine, compilerErrors.Select(d => d.ToString()));
            throw new InvalidOperationException($"Consuming compilation failed with compiler errors:{Environment.NewLine}{errorMessages}");
        }

        var analyzer = new SourceKnownEntityTypeAnalyzer();
        var compilationWithAnalyzers = consumingCompilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(CancellationToken.None);
    }
}
