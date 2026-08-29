namespace DRN.Test.Analyzer.Tests;

public class SourceKnownEntityTypeAnalyzerTests
{
    private const string SharedKernelDomainStubs = """
        namespace DRN.Framework.SharedKernel.Domain
        {
            using System;

            public interface IAppId
            {
                public const byte DefaultAppId = 0;
                public const byte NexusAppId = 126;
                public const byte TestAppId = 127;
                public const byte MaxAppId = 127;
                static abstract byte AppId { get; }
            }

            public readonly struct DefaultApp : IAppId
            {
                public const byte Value = IAppId.DefaultAppId;
                public static byte AppId => Value;
            }

            public readonly struct NexusApp : IAppId
            {
                public const byte Value = IAppId.NexusAppId;
                public static byte AppId => Value;
            }

            public readonly struct TestApp : IAppId
            {
                public const byte Value = IAppId.TestAppId;
                public static byte AppId => Value;
            }

            [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
            public class TestEntityTypeAttribute(byte entityType) : EntityTypeAttribute<TestApp>(entityType);

            [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
            public abstract class EntityTypeAttribute(byte entityType, byte appId) : Attribute
            {
                public byte EntityType { get; } = entityType;
                public byte AppId { get; } = appId;
            }

            [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
            public class EntityTypeAttribute<TApp>(byte entityType)
                : EntityTypeAttribute(entityType, TApp.AppId)
                where TApp : IAppId;

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

    private sealed class CustomOptionsProvider(Dictionary<string, string> globalOptions) : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions _options = new CustomConfigOptions(globalOptions);

        public override AnalyzerConfigOptions GlobalOptions => _options;
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;
    }

    private sealed class CustomConfigOptions(Dictionary<string, string> options) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
            => options.TryGetValue(key, out value!);
    }

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(params string[] sources)
        => await RunAnalyzerAsync(sources, assemblyName: "TestAssembly");

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(
        string[] sources,
        string assemblyName = "TestAssembly",
        Dictionary<string, string>? buildProperties = null)
    {
        var syntaxTrees = sources
            .Select((s, i) => CSharpSyntaxTree.ParseText(s, path: $"Source{i}.cs"))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            assemblyName,
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
        var analyzerOptions = buildProperties != null
            ? new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty, new CustomOptionsProvider(buildProperties))
            : null;

        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer),
            analyzerOptions);

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(CancellationToken.None);
    }

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerWithReferenceAsync(
        string referencedSource,
        string consumingSource,
        string consumingAssemblyName = "ConsumingAssembly",
        Dictionary<string, string>? buildProperties = null)
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
            consumingAssemblyName,
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
        var analyzerOptions = buildProperties != null
            ? new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty, new CustomOptionsProvider(buildProperties))
            : null;

        var compilationWithAnalyzers = consumingCompilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer),
            analyzerOptions);

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ValidEntity_WithEntityType_ProducesNoDiagnostics()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            [EntityType<DefaultApp>(1)]
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

            [EntityType<DefaultApp>(5)]
            public class FirstEntity : SourceKnownEntity;

            [EntityType<DefaultApp>(5)]
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

            [EntityType<DefaultApp>(1)]
            public class FirstEntity : SourceKnownEntity;

            [EntityType<DefaultApp>(2)]
            public class SecondEntity : SourceKnownEntity;

            [EntityType<DefaultApp>(3)]
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

            [EntityType<DefaultApp>(1)]
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

            [EntityType<DefaultApp>(1)]
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
                [EntityType<DefaultApp>(1)]
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

            [EntityType<DefaultApp>((byte)DomainEntityTypes.ItemOne)]
            public class EntityA : SourceKnownEntity;

            [EntityType<DefaultApp>((byte)DomainEntityTypes.ItemDuplicate)]
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
                [EntityType<DefaultApp>(1)]
                public class User : SourceKnownEntity;
            }

            namespace DomainB
            {
                [EntityType<DefaultApp>(2)]
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
                [EntityType<DefaultApp>(1)]
                public class User : SourceKnownEntity;
            }

            namespace Tests
            {
                public class TestFixture
                {
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
                [EntityType<DefaultApp>(42)]
                public class ExternalEntity : SourceKnownEntity;
            }
            """;

        const string consumingSource = """
            using DRN.Framework.SharedKernel.Domain;

            namespace LocalDomain
            {
                [EntityType<DefaultApp>(42)]
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
                [EntityType<DefaultApp>(1)]
                public class Order : SourceKnownEntity;
            }
            """;

        const string consumingSource = """
            using DRN.Framework.SharedKernel.Domain;

            namespace LocalDomain
            {
                [EntityType<DefaultApp>(2)]
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
                [EntityType<DefaultApp>(42)]
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
                [EntityType<DefaultApp>(42)]
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
                [EntityType<DefaultApp>(2)]
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
        DiagnosticDescriptors.MultipleAppIdsNotPermitted.HelpLinkUri.Should().Be(DiagnosticDescriptors.HelpLinkUri);
        DiagnosticDescriptors.UnresolvableAppId.HelpLinkUri.Should().Be(DiagnosticDescriptors.HelpLinkUri);
        DiagnosticDescriptors.AppIdOutOfRange.HelpLinkUri.Should().Be(DiagnosticDescriptors.HelpLinkUri);
        DiagnosticDescriptors.AppIdOutOfRange.CustomTags.Should().Contain(WellKnownDiagnosticTags.CompilationEnd);
    }

    [Fact]
    public async Task DuplicateEntityType_MultipleEntities_ReportsDeterministicallyOnSubsequentDeclarations()
    {
        const string source1 = """
            using DRN.Framework.SharedKernel.Domain;

            [EntityType<DefaultApp>(7)]
            public class AlphaEntity : SourceKnownEntity;
            """;

        const string source2 = """
            using DRN.Framework.SharedKernel.Domain;

            [EntityType<DefaultApp>(7)]
            public class BetaEntity : SourceKnownEntity;

            [EntityType<DefaultApp>(7)]
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
                [EntityType<DefaultApp>(1)]
                public class Customer : SourceKnownEntity;
            }
            """;

        const string source2 = """
            using DRN.Framework.SharedKernel.Domain;

            namespace DomainB
            {
                [EntityType<DefaultApp>(2)]
                public class Customer : SourceKnownEntity;
            }

            namespace DomainC
            {
                [EntityType<DefaultApp>(3)]
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
                    [EntityType<DefaultApp>(42)]
                    public class EntityA : SourceKnownEntity;
                }
                """),
            ("ReferencedAssemblyB", """
                using DRN.Framework.SharedKernel.Domain;

                namespace DomainB
                {
                    [EntityType<DefaultApp>(42)]
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
                    [EntityType<DefaultApp>(1)]
                    public class Product : SourceKnownEntity;
                }
                """),
            ("ReferencedAssemblyB", """
                using DRN.Framework.SharedKernel.Domain;

                namespace DomainB
                {
                    [EntityType<DefaultApp>(2)]
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
                    [EntityType<DefaultApp>(10)]
                    public class SharedEntity : SourceKnownEntity;
                }
                """),
            ("ReferencedAssemblyA", """
                using DRN.Framework.SharedKernel.Domain;
                using CommonDomain;

                namespace DomainA
                {
                    [EntityType<DefaultApp>(20)]
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
                    [EntityType<DefaultApp>(30)]
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

    [Fact]
    public async Task DuplicateEntityType_WithDifferentAppId_ProducesNoDiagnostics()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public readonly struct App5 : IAppId
            {
                public const byte Value = 5;
                public static byte AppId => Value;
            }

            public readonly struct App6 : IAppId
            {
                public const byte Value = 6;
                public static byte AppId => Value;
            }

            [EntityType<App5>(1)]
            public class FirstEntity : SourceKnownEntity;

            [EntityType<App6>(1)]
            public class SecondEntity : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerAsync([testCode], assemblyName: "Sample.Test.Unit");

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DuplicateEntityType_WithSameAppId_ProducesDRN0002()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public readonly struct App5 : IAppId
            {
                public const byte Value = 5;
                public static byte AppId => Value;
            }

            [EntityType<App5>(1)]
            public class FirstEntity : SourceKnownEntity;

            [EntityType<App5>(1)]
            public class SecondEntity : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DRN0002");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("1").And.Contain("5");
    }

    [Fact]
    public async Task DuplicateEntityName_WithDifferentAppId_ProducesNoDiagnostics()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public readonly struct App5 : IAppId
            {
                public const byte Value = 5;
                public static byte AppId => Value;
            }

            public readonly struct App6 : IAppId
            {
                public const byte Value = 6;
                public static byte AppId => Value;
            }

            namespace DomainA
            {
                [EntityType<App5>(1)]
                public class Customer : SourceKnownEntity;
            }

            namespace DomainB
            {
                [EntityType<App6>(1)]
                public class Customer : SourceKnownEntity;
            }
            """;

        var diagnostics = await RunAnalyzerAsync([testCode], assemblyName: "Sample.Test.Unit");

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DuplicateEntityName_WithSameAppId_ProducesDRN0004()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public readonly struct App5 : IAppId
            {
                public const byte Value = 5;
                public static byte AppId => Value;
            }

            namespace DomainA
            {
                [EntityType<App5>(1)]
                public class Customer : SourceKnownEntity;
            }

            namespace DomainB
            {
                [EntityType<App5>(2)]
                public class Customer : SourceKnownEntity;
            }
            """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DRN0004");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostics[0].GetMessage().Should().Contain("Customer").And.Contain("5");
    }

    [Fact]
    public async Task GenericEntityTypeAttribute_WithDifferentIAppId_ProducesNoDiagnostics()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public readonly struct CustomNexusApp : IAppId
            {
                public const byte Value = 5;
                public static byte AppId => Value;
            }

            public readonly struct SampleApp : IAppId
            {
                public const byte Value = 6;
                public static byte AppId => Value;
            }

            [EntityType<CustomNexusApp>(1)]
            public class NexusDevice : SourceKnownEntity;

            [EntityType<SampleApp>(1)]
            public class SampleAuthor : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerAsync([testCode], assemblyName: "Sample.Test.Unit");

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task GenericEntityTypeAttribute_WithSameIAppId_ProducesDRN0002()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public readonly struct CustomNexusApp : IAppId
            {
                public const byte Value = 5;
                public static byte AppId => Value;
            }

            [EntityType<CustomNexusApp>(1)]
            public class FirstDevice : SourceKnownEntity;

            [EntityType<CustomNexusApp>(1)]
            public class SecondDevice : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DRN0002");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("1").And.Contain("5");
    }

    [Fact]
    public async Task DerivedEntityTypeAttribute_FromGenericEntityType_WorksAndSeparatesAppId()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public readonly struct CustomNexusApp : IAppId
            {
                public const byte Value = 5;
                public static byte AppId => Value;
            }

            public sealed class CustomNexusEntityTypeAttribute(byte entityType) : EntityTypeAttribute<CustomNexusApp>(entityType);

            public readonly struct SampleApp : IAppId
            {
                public const byte Value = 6;
                public static byte AppId => Value;
            }

            public sealed class SampleEntityTypeAttribute(byte entityType) : EntityTypeAttribute<SampleApp>(entityType);

            [CustomNexusEntityType(1)]
            public class NexusDevice : SourceKnownEntity;

            [SampleEntityType(1)]
            public class SampleAuthor : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerAsync([testCode], assemblyName: "Sample.Test.Unit");

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DerivedEntityTypeAttribute_WithSameAppId_ProducesDRN0002()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public readonly struct CustomNexusApp : IAppId
            {
                public const byte Value = 5;
                public static byte AppId => Value;
            }

            public sealed class CustomNexusEntityTypeAttribute(byte entityType) : EntityTypeAttribute<CustomNexusApp>(entityType);

            [CustomNexusEntityType(1)]
            public class FirstDevice : SourceKnownEntity;

            [CustomNexusEntityType(1)]
            public class SecondDevice : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DRN0002");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("1").And.Contain("5");
    }

    [Fact]
    public async Task AppId_FromOuterClassConstField_ResolvesCorrectly()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public class TestContainer
            {
                private const byte TestAppId = 5;

                public readonly struct NestedApp : IAppId
                {
                    public const byte Value = TestAppId;
                    public static byte AppId => Value;
                }

                [EntityType<NestedApp>(1)]
                public class NestedEntity : SourceKnownEntity;

                [EntityType<DefaultApp>(1)]
                public class DefaultEntity : SourceKnownEntity;
            }
            """;

        var diagnostics = await RunAnalyzerAsync([testCode], assemblyName: "Sample.Test.Unit");

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task BuiltInAppPartitions_DefaultApp_NexusApp_TestApp_SeparateCleanly()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            [EntityType<DefaultApp>(1)]
            public class DefaultEntity : SourceKnownEntity;

            [EntityType<NexusApp>(1)]
            public class NexusEntity : SourceKnownEntity;

            [TestEntityType(1)]
            public class TestEntity : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerAsync([testCode], assemblyName: "Sample.Test.Unit");

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ReferencedAssembly_WithSameCustomAppId_ProducesDRN0002()
    {
        var referencedSources = new (string AssemblyName, string Source)[]
        {
            ("ReferencedAssembly", """
                using DRN.Framework.SharedKernel.Domain;

                namespace ExternalDomain
                {
                    public readonly struct App5 : IAppId
                    {
                        public const byte AppId = 5;
                        static byte IAppId.AppId => AppId;
                    }

                    [EntityType<App5>(42)]
                    public class ExternalEntity : SourceKnownEntity;
                }
                """)
        };

        const string consumingSource = """
            using DRN.Framework.SharedKernel.Domain;
            using ExternalDomain;

            namespace LocalDomain
            {
                [EntityType<App5>(42)]
                public class LocalEntity : SourceKnownEntity;
            }
            """;

        var diagnostics = await RunAnalyzerWithMultipleReferencesAsync(referencedSources, consumingSource);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DRN0002");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("42");
        diagnostics[0].GetMessage().Should().Contain("5");
        diagnostics[0].GetMessage().Should().Contain("ExternalEntity");
    }

    [Fact]
    public async Task ReferencedAssembly_WithDifferentCustomAppId_ProducesNoDiagnostics()
    {
        var referencedSources = new (string AssemblyName, string Source)[]
        {
            ("ReferencedAssembly", """
                using DRN.Framework.SharedKernel.Domain;

                namespace ExternalDomain
                {
                    public readonly struct App5 : IAppId
                    {
                        public const byte AppId = 5;
                        static byte IAppId.AppId => AppId;
                    }

                    [EntityType<App5>(42)]
                    public class ExternalEntity : SourceKnownEntity;
                }
                """)
        };

        const string consumingSource = """
            using DRN.Framework.SharedKernel.Domain;

            namespace LocalDomain
            {
                public readonly struct App6 : IAppId
                {
                    public const byte Value = 6;
                    public static byte AppId => Value;
                }

                [EntityType<App6>(42)]
                public class LocalEntity : SourceKnownEntity;
            }
            """;

        var diagnostics = await RunAnalyzerWithMultipleReferencesAsync(
            referencedSources,
            consumingSource,
            consumingAssemblyName: "Sample.Test.Unit");

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ReferencedAssembly_WithDerivedEntityTypeAttribute_ProducesDRN0002_WhenAppIdMatches()
    {
        var referencedSources = new (string AssemblyName, string Source)[]
        {
            ("ReferencedAssembly", """
                using DRN.Framework.SharedKernel.Domain;

                namespace ExternalDomain
                {
                    public readonly struct App5 : IAppId
                    {
                        public const byte AppId = 5;
                        static byte IAppId.AppId => AppId;
                    }

                    public sealed class App5EntityTypeAttribute(byte entityType) : EntityTypeAttribute<App5>(entityType);

                    [App5EntityType(42)]
                    public class ExternalEntity : SourceKnownEntity;
                }
                """)
        };

        const string consumingSource = """
            using DRN.Framework.SharedKernel.Domain;
            using ExternalDomain;

            namespace LocalDomain
            {
                [EntityType<App5>(42)]
                public class LocalEntity : SourceKnownEntity;
            }
            """;

        var diagnostics = await RunAnalyzerWithMultipleReferencesAsync(referencedSources, consumingSource);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DRN0002");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("42");
        diagnostics[0].GetMessage().Should().Contain("5");
    }

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerWithMultipleReferencesAsync(
        (string AssemblyName, string Source)[] referencedSources,
        string consumingSource = "",
        string consumingAssemblyName = "ConsumingAssembly",
        Dictionary<string, string>? buildProperties = null)
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
            consumingAssemblyName,
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
        var analyzerOptions = buildProperties != null
            ? new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty, new CustomOptionsProvider(buildProperties))
            : null;

        var compilationWithAnalyzers = consumingCompilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer),
            analyzerOptions);

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(CancellationToken.None);
    }

    [Fact]
    public async Task MultipleAppIds_InProductionAssembly_ProducesDRN0005()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public readonly struct App1 : IAppId
            {
                public const byte Value = 1;
                public static byte AppId => Value;
            }

            public readonly struct App2 : IAppId
            {
                public const byte Value = 2;
                public static byte AppId => Value;
            }

            [EntityType<App1>(1)]
            public class FirstEntity : SourceKnownEntity;

            [EntityType<App2>(2)]
            public class SecondEntity : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DRN0005");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("TestAssembly").And.Contain("1, 2");
    }

    [Fact]
    public async Task MultipleAppIds_InTestAssembly_ProducesNoDRN0005()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public readonly struct App1 : IAppId
            {
                public const byte Value = 1;
                public static byte AppId => Value;
            }

            public readonly struct App2 : IAppId
            {
                public const byte Value = 2;
                public static byte AppId => Value;
            }

            [EntityType<App1>(1)]
            public class FirstEntity : SourceKnownEntity;

            [EntityType<App2>(2)]
            public class SecondEntity : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerAsync([testCode], assemblyName: "Sample.Test.Unit");

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task MultipleAppIds_WithAllowMultipleAppIdsBuildProperty_ProducesNoDRN0005()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public readonly struct App1 : IAppId
            {
                public const byte Value = 1;
                public static byte AppId => Value;
            }

            public readonly struct App2 : IAppId
            {
                public const byte Value = 2;
                public static byte AppId => Value;
            }

            [EntityType<App1>(1)]
            public class FirstEntity : SourceKnownEntity;

            [EntityType<App2>(2)]
            public class SecondEntity : SourceKnownEntity;
            """;

        var buildProperties = new Dictionary<string, string>
        {
            ["build_property.AllowMultipleAppIds"] = "true"
        };

        var diagnostics = await RunAnalyzerAsync([testCode], assemblyName: "ProductionAssembly", buildProperties: buildProperties);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task MultipleAppIds_WithIsTestProjectBuildProperty_ProducesNoDRN0005()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public readonly struct App1 : IAppId
            {
                public const byte Value = 1;
                public static byte AppId => Value;
            }

            public readonly struct App2 : IAppId
            {
                public const byte Value = 2;
                public static byte AppId => Value;
            }

            [EntityType<App1>(1)]
            public class FirstEntity : SourceKnownEntity;

            [EntityType<App2>(2)]
            public class SecondEntity : SourceKnownEntity;
            """;

        var buildProperties = new Dictionary<string, string>
        {
            ["build_property.IsTestProject"] = "true"
        };

        var diagnostics = await RunAnalyzerAsync([testCode], assemblyName: "CustomTestRunner", buildProperties: buildProperties);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task MultipleAppIds_WithMtpRunnerBuildProperty_ProducesNoDRN0005()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public readonly struct App1 : IAppId
            {
                public const byte Value = 1;
                public static byte AppId => Value;
            }

            public readonly struct App2 : IAppId
            {
                public const byte Value = 2;
                public static byte AppId => Value;
            }

            [EntityType<App1>(1)]
            public class FirstEntity : SourceKnownEntity;

            [EntityType<App2>(2)]
            public class SecondEntity : SourceKnownEntity;
            """;

        var buildProperties = new Dictionary<string, string>
        {
            ["build_property.UseMicrosoftTestingPlatformRunner"] = "true"
        };

        var diagnostics = await RunAnalyzerAsync([testCode], assemblyName: "MtpRunnerAssembly", buildProperties: buildProperties);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task TestAppId_WithProductionAppId_ProducesNoDRN0005()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            [EntityType<DefaultApp>(1)]
            public class ProdEntity : SourceKnownEntity;

            [TestEntityType(2)]
            public class TestEntity : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerAsync([testCode], assemblyName: "ProductionAssembly");

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task MultipleAppIds_AcrossReferencedAssembly_ProducesDRN0005()
    {
        const string referencedCode = """
            using DRN.Framework.SharedKernel.Domain;

            public readonly struct App1 : IAppId
            {
                public const byte Value = 1;
                public static byte AppId => Value;
            }

            [EntityType<App1>(1)]
            public class RefEntity : SourceKnownEntity;
            """;

        const string consumingCode = """
            using DRN.Framework.SharedKernel.Domain;

            public readonly struct App2 : IAppId
            {
                public const byte Value = 2;
                public static byte AppId => Value;
            }

            [EntityType<App2>(2)]
            public class ConsumingEntity : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerWithReferenceAsync(referencedCode, consumingCode);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DRN0005");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("ConsumingAssembly").And.Contain("1, 2");
    }

    [Fact]
    public async Task MultipleAppIds_WithTestingReferenceInProductionAssembly_ProducesDRN0005()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public readonly struct App1 : IAppId
            {
                public const byte Value = 1;
                public static byte AppId => Value;
            }

            public readonly struct App2 : IAppId
            {
                public const byte Value = 2;
                public static byte AppId => Value;
            }

            [EntityType<App1>(1)]
            public class FirstEntity : SourceKnownEntity;

            [EntityType<App2>(2)]
            public class SecondEntity : SourceKnownEntity;
            """;

        const string testingCode = "public class DummyTesting;";
        var diagnostics = await RunAnalyzerWithMultipleReferencesAsync(
            [("DRN.Framework.Testing", testingCode)],
            testCode,
            consumingAssemblyName: "ProductionAssembly");

        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DRN0005");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("ProductionAssembly").And.Contain("1, 2");
    }

    [Fact]
    public async Task ReferencedAssembly_WithPropertyOnlyIAppId_ProducesDRN0006()
    {
        var referencedSources = new (string AssemblyName, string Source)[]
        {
            ("ReferencedAssembly", """
                using DRN.Framework.SharedKernel.Domain;

                namespace ExternalDomain
                {
                    public readonly struct PropertyOnlyApp : IAppId
                    {
                        public static byte AppId => 5;
                    }

                    [EntityType<PropertyOnlyApp>(42)]
                    public class ExternalEntity : SourceKnownEntity;
                }
                """)
        };

        const string consumingSource = """
            using DRN.Framework.SharedKernel.Domain;
            using ExternalDomain;

            namespace LocalDomain
            {
                [EntityType<DefaultApp>(1)]
                public class LocalEntity : SourceKnownEntity;
            }
            """;

        var diagnostics = await RunAnalyzerWithMultipleReferencesAsync(
            referencedSources,
            consumingSource,
            consumingAssemblyName: "ConsumingAssembly");

        diagnostics.Should().ContainSingle(d => d.Id == "DRN0006" && d.Severity == DiagnosticSeverity.Error);
        diagnostics.First(d => d.Id == "DRN0006").GetMessage().Should().Contain("ExternalEntity");
    }

    [Fact]
    public async Task LocalEntity_WithUnresolvableIAppId_ProducesDRN0006()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public readonly struct UnresolvableApp : IAppId
            {
                private static byte Compute() => 5;
                public static byte AppId => Compute();
            }

            [EntityType<UnresolvableApp>(1)]
            public class BadEntity : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().ContainSingle(d => d.Id == "DRN0006" && d.Severity == DiagnosticSeverity.Error);
        diagnostics.First(d => d.Id == "DRN0006").GetMessage().Should().Contain("BadEntity");
    }

    [Fact]
    public async Task LocalEntity_WithPropertyOnlyIAppId_ProducesDRN0006()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public readonly struct PropertyOnlyApp : IAppId
            {
                public static byte AppId => 5;
            }

            [EntityType<PropertyOnlyApp>(1)]
            public class BadEntity : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().ContainSingle(d => d.Id == "DRN0006" && d.Severity == DiagnosticSeverity.Error);
        diagnostics.First(d => d.Id == "DRN0006").GetMessage().Should().Contain("BadEntity");
    }

    [Fact]
    public async Task LocalEntity_WithConflictingConstantAndProperty_ProducesDRN0006()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public readonly struct ConflictingApp : IAppId
            {
                public const byte Value = 5;
                public static byte AppId => 6;
            }

            [EntityType<ConflictingApp>(1)]
            public class BadEntity : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().ContainSingle(d => d.Id == "DRN0006" && d.Severity == DiagnosticSeverity.Error);
        diagnostics.First(d => d.Id == "DRN0006").GetMessage().Should().Contain("BadEntity");
    }

    [Fact]
    public async Task LocalEntity_WithMatchingConstantAndProperty_CompilesCleanly()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public readonly struct GoodApp : IAppId
            {
                public const byte Value = 5;
                public static byte AppId => Value;
            }

            [EntityType<GoodApp>(1)]
            public class GoodEntity : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task LocalEntity_WithQualifiedConstantProperty_MatchingConstant_CompilesCleanly()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public static class Other
            {
                public const byte Value = 6;
            }

            public readonly struct App : IAppId
            {
                public const byte Value = Other.Value;
                public static byte AppId => Other.Value;
            }

            [EntityType<App>(1)]
            public class GoodEntity : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task LocalEntity_WithQualifiedConstantProperty_MismatchedConstant_ProducesDRN0006()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public static class Other
            {
                public const byte Value = 6;
            }

            public readonly struct App : IAppId
            {
                public const byte Value = 5;
                public static byte AppId => Other.Value;
            }

            [EntityType<App>(1)]
            public class BadEntity : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().ContainSingle(d => d.Id == "DRN0006" && d.Severity == DiagnosticSeverity.Error);
        diagnostics.First(d => d.Id == "DRN0006").GetMessage().Should().Contain("BadEntity");
    }

    [Fact]
    public async Task LocalEntity_WithSelfRecursiveProperty_ProducesDRN0006()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public readonly struct RecursiveApp : IAppId
            {
                public const byte Value = 5;
                public static byte AppId => AppId;
            }

            [EntityType<RecursiveApp>(1)]
            public class BadEntity : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().ContainSingle(d => d.Id == "DRN0006" && d.Severity == DiagnosticSeverity.Error);
        diagnostics.First(d => d.Id == "DRN0006").GetMessage().Should().Contain("BadEntity");
    }

    [Fact]
    public async Task LocalEntity_WithMutuallyRecursiveProperties_ProducesDRN0006()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public readonly struct MutualApp : IAppId
            {
                public const byte Value = 5;
                public static byte OtherProp => AppId;
                public static byte AppId => OtherProp;
            }

            [EntityType<MutualApp>(1)]
            public class BadEntity : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().ContainSingle(d => d.Id == "DRN0006" && d.Severity == DiagnosticSeverity.Error);
        diagnostics.First(d => d.Id == "DRN0006").GetMessage().Should().Contain("BadEntity");
    }

    [Fact]
    public async Task LocalEntity_WithNestedIAppId_UsingOuterConstantWithoutOwnConstant_ProducesDRN0006()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public static class OuterScope
            {
                public const byte Value = 5;

                public readonly struct NestedApp : IAppId
                {
                    public static byte AppId => Value;
                }
            }

            [EntityType<OuterScope.NestedApp>(1)]
            public class BadEntity : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().ContainSingle(d => d.Id == "DRN0006" && d.Severity == DiagnosticSeverity.Error);
        diagnostics.First(d => d.Id == "DRN0006").GetMessage().Should().Contain("BadEntity");
    }

    [Fact]
    public async Task LocalEntity_WithNonPublicConstant_ProducesDRN0006()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public readonly struct PrivateConstApp : IAppId
            {
                private const byte Value = 5;
                public static byte AppId => Value;
            }

            [EntityType<PrivateConstApp>(1)]
            public class BadEntity : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().ContainSingle(d => d.Id == "DRN0006" && d.Severity == DiagnosticSeverity.Error);
        diagnostics.First(d => d.Id == "DRN0006").GetMessage().Should().Contain("BadEntity");
    }

    [Fact]
    public async Task LocalEntity_WithNestedIAppId_WithOwnPublicConstant_CompilesCleanly()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public static class OuterScope
            {
                public const byte Value = 99;

                public readonly struct NestedApp : IAppId
                {
                    public const byte Value = 5;
                    public static byte AppId => Value;
                }
            }

            [EntityType<OuterScope.NestedApp>(1)]
            public class GoodEntity : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task LocalEntity_WithOutOfRangeAppId_ProducesDRN0007()
    {
        const string testCode = """
            using DRN.Framework.SharedKernel.Domain;

            public readonly struct OutOfRangeApp : IAppId
            {
                public const byte Value = 128;
                public static byte AppId => Value;
            }

            [EntityType<OutOfRangeApp>(1)]
            public class BadEntity : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().ContainSingle(d => d.Id == "DRN0007" && d.Severity == DiagnosticSeverity.Error);
        diagnostics.First(d => d.Id == "DRN0007").GetMessage().Should().Contain("BadEntity").And.Contain("128");
    }

    [Fact]
    public async Task ReferencedEntity_WithOutOfRangeAppId_ProducesDRN0007()
    {
        var referencedSources = new (string AssemblyName, string Source)[]
        {
            ("ReferencedAssembly", """
                using DRN.Framework.SharedKernel.Domain;

                namespace ExternalDomain
                {
                    public readonly struct OutOfRangeApp : IAppId
                    {
                        public const byte Value = 255;
                        public static byte AppId => Value;
                    }

                    [EntityType<OutOfRangeApp>(1)]
                    public class ExternalEntity : SourceKnownEntity;
                }
                """)
        };

        var diagnostics = await RunAnalyzerWithMultipleReferencesAsync(referencedSources);

        diagnostics.Should().ContainSingle(d => d.Id == "DRN0007" && d.Severity == DiagnosticSeverity.Error);
        diagnostics.First(d => d.Id == "DRN0007").GetMessage().Should().Contain("ExternalEntity").And.Contain("255");
    }

    [Fact]
    public async Task MultipleAppIds_AcrossThreeLevelTransitiveAssembly_ProducesDRN0005()
    {
        var referencedSources = new (string AssemblyName, string Source)[]
        {
            ("DomainBaseAssembly", """
                using DRN.Framework.SharedKernel.Domain;

                namespace DomainBase
                {
                    public readonly struct TransitiveApp1 : IAppId
                    {
                        public const byte Value = 10;
                        public static byte AppId => Value;
                    }

                    public abstract class BaseDomainEntity : SourceKnownEntity;

                    public sealed class CustomEntityTypeAttribute(byte entityType) : EntityTypeAttribute<TransitiveApp1>(entityType);
                }
                """),
            ("DomainSubmoduleAssembly", """
                namespace DomainSubmodule
                {
                    [DomainBase.CustomEntityType(1)]
                    public class TransitiveEntity : DomainBase.BaseDomainEntity;
                }
                """)
        };

        const string consumingCode = """
            using DRN.Framework.SharedKernel.Domain;

            public readonly struct App2 : IAppId
            {
                public const byte Value = 20;
                public static byte AppId => Value;
            }

            [EntityType<App2>(2)]
            public class ConsumingEntity : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerWithMultipleReferencesAsync(
            referencedSources,
            consumingSource: consumingCode,
            consumingAssemblyName: "ProductionAssembly");

        diagnostics.Should().ContainSingle(d => d.Id == "DRN0005" && d.Severity == DiagnosticSeverity.Error);
        diagnostics.First(d => d.Id == "DRN0005").GetMessage().Should().Contain("ProductionAssembly").And.Contain("10, 20");
    }

    [Fact]
    public async Task DuplicateEntityType_AcrossThreeLevelTransitiveAssembly_ProducesDRN0002()
    {
        var referencedSources = new (string AssemblyName, string Source)[]
        {
            ("DomainBaseAssembly", """
                using DRN.Framework.SharedKernel.Domain;

                namespace DomainBase
                {
                    public readonly struct SharedApp : IAppId
                    {
                        public const byte Value = 42;
                        public static byte AppId => Value;
                    }

                    public abstract class BaseDomainEntity : SourceKnownEntity;

                    public sealed class CustomEntityTypeAttribute(byte entityType) : EntityTypeAttribute<SharedApp>(entityType);
                }
                """),
            ("DomainSubmoduleAssembly", """
                namespace DomainSubmodule
                {
                    [DomainBase.CustomEntityType(5)]
                    public class TransitiveEntity : DomainBase.BaseDomainEntity;
                }
                """)
        };

        const string consumingCode = """
            using DRN.Framework.SharedKernel.Domain;

            [EntityType<DomainBase.SharedApp>(5)]
            public class LocalCollidingEntity : SourceKnownEntity;
            """;

        var diagnostics = await RunAnalyzerWithMultipleReferencesAsync(
            referencedSources,
            consumingSource: consumingCode,
            consumingAssemblyName: "ProductionAssembly");

        diagnostics.Should().ContainSingle(d => d.Id == "DRN0002" && d.Severity == DiagnosticSeverity.Error);
        diagnostics.First(d => d.Id == "DRN0002").GetMessage().Should().Contain("5").And.Contain("42");
    }
}
