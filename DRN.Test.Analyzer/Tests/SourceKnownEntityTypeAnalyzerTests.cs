using System.Diagnostics.CodeAnalysis;
using static DRN.Test.Analyzer.Tests.EntityAnalyzerTestFixture;

namespace DRN.Test.Analyzer.Tests;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public sealed class SourceKnownEntityTypeAnalyzerTests
{
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
    public async Task MissingEntityType_OnDirectSourceKnownEntityDescendant_ProducesDRNENTITY001()
    {
        const string testCode = """
                                using DRN.Framework.SharedKernel.Domain;

                                public class MissingAttributeEntity : SourceKnownEntity;
                                """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DRNENTITY001");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("MissingAttributeEntity");
    }

    [Fact]
    public async Task MissingEntityType_OnIndirectDescendant_ProducesDRNENTITY001()
    {
        const string testCode = """
                                using DRN.Framework.SharedKernel.Domain;

                                public abstract class BaseDomainEntity : AggregateRoot;

                                public class ChildEntity : BaseDomainEntity;
                                """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DRNENTITY001");
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
    public async Task DuplicateEntityType_AcrossTwoEntities_ProducesDRNENTITY002()
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
        diagnostics[0].Id.Should().Be("DRNENTITY002");
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
    public async Task InvalidUsage_EntityTypeOnAbstractClass_ProducesDRNENTITY003()
    {
        const string testCode = """
                                using DRN.Framework.SharedKernel.Domain;

                                [EntityType<DefaultApp>(1)]
                                public abstract class AbstractEntityWithAttribute : SourceKnownEntity;
                                """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DRNENTITY003");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("AbstractEntityWithAttribute");
    }

    [Fact]
    public async Task InvalidUsage_EntityTypeOnNonSourceKnownEntity_ProducesDRNENTITY003()
    {
        const string testCode = """
                                using DRN.Framework.SharedKernel.Domain;

                                [EntityType<DefaultApp>(1)]
                                public class RegularNonEntityClass;
                                """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DRNENTITY003");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("RegularNonEntityClass");
    }

    [Fact]
    public async Task InvalidUsage_EntityTypeOnPrivateClass_ProducesDRNENTITY003()
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
        diagnostics[0].Id.Should().Be("DRNENTITY003");
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
        diagnostics[0].Id.Should().Be("DRNENTITY002");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("10");
    }

    [Fact]
    public async Task DuplicateEntityName_AcrossDifferentNamespaces_ProducesDRNENTITY004()
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
        diagnostics[0].Id.Should().Be("DRNENTITY004");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostics[0].GetMessage().Should().Contain("User");
    }

    [Fact]
    public async Task PrivateNestedEntity_DoesNotProduceDRNENTITY004()
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
    public async Task DuplicateEntityType_AcrossReferencedAssembly_ProducesDRNENTITY002()
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
        diagnostics[0].Id.Should().Be("DRNENTITY002");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("42");
        diagnostics[0].GetMessage().Should().Contain("ExternalEntity");
    }

    [Fact]
    public async Task DuplicateEntityName_AcrossReferencedAssembly_ProducesDRNENTITY004()
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
        diagnostics[0].Id.Should().Be("DRNENTITY004");
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

    [Theory]
    [DataInlineUnit(42)]
    [DataInlineUnit(2)]
    public async Task PublicConsumingEntity_DoesNotCollide_WithPrivateNestedEntityInReferencedAssembly(byte entityType)
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

        var consumingSource = $$"""
                                using DRN.Framework.SharedKernel.Domain;

                                namespace LocalDomain
                                {
                                    [EntityType<DefaultApp>({{entityType}})]
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
        DiagnosticDescriptors.UnsupportedEntityAttribute.HelpLinkUri.Should().Be(DiagnosticDescriptors.HelpLinkUri);
        new SourceKnownEntityTypeAnalyzer().SupportedDiagnostics.Should().Contain(DiagnosticDescriptors.UnsupportedEntityAttribute);
    }

    [Fact]
    public async Task DerivedAttribute_UnsupportedConstructors_AreRejectedAtDeclaration()
    {
        string[] declarations =
        [
            "public sealed class CustomAttribute(byte value) : EntityTypeAttribute<DefaultApp>((byte)(value + 1));",
            "public sealed class CustomAttribute(byte ignored, byte entityType) : EntityTypeAttribute<DefaultApp>(entityType);",
            "public sealed class CustomAttribute(byte entityType, byte unrelated) : EntityTypeAttribute<DefaultApp>(entityType);",
            "public sealed class CustomAttribute() : EntityTypeAttribute<DefaultApp>(42);",
            "public sealed class CustomAttribute(byte entityType) : EntityTypeAttribute<DefaultApp>(42);",
            "public sealed class CustomAttribute(byte entityType) : EntityTypeAttribute(entityType, 5);",
            "public sealed class CustomAttribute : EntityTypeAttribute<DefaultApp> { public CustomAttribute(byte value) : base((byte)(value + 1)) {} }",
            "public sealed class CustomAttribute : EntityTypeAttribute<DefaultApp> { public CustomAttribute() : base(1) {} public CustomAttribute(byte value) : base(value) {} }"
        ];

        foreach (var declaration in declarations)
        {
            var diagnostics = await RunAnalyzerAsync("using DRN.Framework.SharedKernel.Domain; " + declaration);
            diagnostics.Should().ContainSingle(d => d.Id == "DRNENTITY008", declaration);
            diagnostics.Should().HaveCount(1);
        }
    }

    [Fact]
    public async Task DerivedAttribute_ConstructedAmbiguousPrimaryBaseInitializers_Should_Not_Crash()
    {
        const string source = """
                              using DRN.Framework.SharedKernel.Domain;
                              public interface IOther;
                              public sealed class CustomAttribute(byte value) : EntityTypeAttribute<DefaultApp>(value), IOther;
                              """;
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = await tree.GetRootAsync();
        var initializer = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.PrimaryConstructorBaseTypeSyntax>().Single();
        var otherBase = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.SimpleBaseTypeSyntax>().Single();
        // The parser only creates a primary-constructor initializer for the first base type.
        // Construct the malformed tree directly to exercise the multiple-initializer guard.
        var ambiguousBase = SyntaxFactory.PrimaryConstructorBaseType(otherBase.Type, initializer.ArgumentList);
        tree = tree.WithRootAndOptions(root.ReplaceNode(otherBase, ambiguousBase), tree.Options);
        (await tree.GetRootAsync()).DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.PrimaryConstructorBaseTypeSyntax>()
            .Should().HaveCount(2);
        var compilation = CSharpCompilation.Create("Domain", [tree], GetBaseReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        EntityAttributeContract.IsSupported(compilation.GetTypeByMetadataName("CustomAttribute"), compilation, CancellationToken.None)
            .Should().BeFalse();
        var diagnostics = await compilation.WithAnalyzers([new SourceKnownEntityTypeAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        diagnostics.Should().NotContain(diagnostic => diagnostic.Id == "AD0001");
        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == "DRNENTITY008");
    }

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    [SuppressMessage("ReSharper", "AccessToDisposedClosure",
        Justification = "Both delegates execute synchronously in Throw assertions and finish before the cancellation source is disposed.")]
    public void EntityMetadataExtraction_ObservesCancellation(bool warmCache)
    {
        const string source = """
                              using DRN.Framework.SharedKernel.Domain;
                              public readonly struct App : IAppId
                              {
                                  public const byte Value = 5;
                                  public static byte AppId => (byte)Partition;
                                  private static int Partition => Value;
                              }
                              [EntityType<App>(42)] public class Entity : SourceKnownEntity;
                              """;
        var compilation = CSharpCompilation.Create("Domain",
            [CSharpSyntaxTree.ParseText(source)], GetBaseReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.AssertNoCompilerErrors();
        var attribute = compilation.GetTypeByMetadataName("Entity")!.GetAttributes().Single();
        var metadata = new EntityAnalyzerHelper.CompilationMetadata(compilation);
        metadata.Read(attribute, CancellationToken.None, out var entityType, out var appId)
            .Should().Be(EntityAnalyzerHelper.MetadataStatus.Resolved);
        entityType.Should().Be(42);
        appId.Should().Be(5);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        if (!warmCache) metadata = new EntityAnalyzerHelper.CompilationMetadata(compilation);
        Action extract = () => metadata.Read(attribute, cancellation.Token, out _, out _);
        extract.Should().Throw<OperationCanceledException>().Which.CancellationToken.Should().Be(cancellation.Token);

        var entityBase = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.SourceKnownEntity")!;
        var facts = new EntityAnalyzerHelper.EntityTypeFacts(entityBase);
        Action scan = () => _ = ReferencedEntityDiscovery.Find(compilation, entityBase, facts, cancellation.Token).ToArray();
        scan.Should().Throw<OperationCanceledException>().Which.CancellationToken.Should().Be(cancellation.Token);
    }

    [Fact]
    public async Task DerivedAttribute_TransformedValue_DoesNotProduceGuessedCollision()
    {
        const string source = """
                              using DRN.Framework.SharedKernel.Domain;
                              public sealed class CustomAttribute(byte value) : EntityTypeAttribute<DefaultApp>((byte)(value + 1));
                              [Custom(1)] public class First : SourceKnownEntity;
                              [EntityType<DefaultApp>(1)] public class Second : SourceKnownEntity;
                              """;

        var diagnostics = await RunAnalyzerAsync(source);
        diagnostics.Should().HaveCount(2); // Producer declaration and entity use.
        diagnostics.Should().OnlyContain(d => d.Id == "DRNENTITY008");
    }

    [Fact]
    public async Task DerivedAttribute_ForwardedEnumChain_DetectsCollisionLocallyAndFromMetadata()
    {
        const string producer = """
                                using DRN.Framework.SharedKernel.Domain;
                                public enum Kinds : byte { First = 42 }
                                public class DomainAttribute<TApp>(byte value) : EntityTypeAttribute<TApp>(value) where TApp : IAppId;
                                public sealed class CustomAttribute : DomainAttribute<DefaultApp>
                                {
                                    public CustomAttribute(Kinds kind) : base((byte)(kind)) {}
                                }
                                [Custom(Kinds.First)] public class First : SourceKnownEntity;
                                """;
        const string consumer = """
                                using DRN.Framework.SharedKernel.Domain;
                                [EntityType<DefaultApp>(42)] public class Second : SourceKnownEntity;
                                """;

        var localDiagnostics = await RunAnalyzerAsync(producer, consumer);
        localDiagnostics.Should().ContainSingle(d => d.Id == "DRNENTITY002");
        localDiagnostics.Should().HaveCount(1);
        var metadataDiagnostics = await RunAnalyzerWithReferenceAsync(producer, consumer);
        metadataDiagnostics.Should().ContainSingle(d => d.Id == "DRNENTITY002");
        metadataDiagnostics.Should().HaveCount(1);
    }

    [Fact]
    public async Task ReferencedAttribute_UnsupportedSignature_IsRejectedForLocalAndReferencedEntities()
    {
        const string producer = """
                                using DRN.Framework.SharedKernel.Domain;
                                public sealed class CustomAttribute(byte ignored, byte entityType) : EntityTypeAttribute<DefaultApp>(entityType);
                                [Custom(1, 42)] public class First : SourceKnownEntity;
                                """;
        const string consumer = """
                                using DRN.Framework.SharedKernel.Domain;
                                [Custom(2, 42)] public class Second : SourceKnownEntity;
                                """;

        var diagnostics = await RunAnalyzerWithReferenceAsync(producer, consumer);
        diagnostics.Should().HaveCount(2);
        diagnostics.Should().OnlyContain(d => d.Id == "DRNENTITY008");
        diagnostics.Should().ContainSingle(d => d.Location == Location.None);
        diagnostics.Should().ContainSingle(d => d.Location.IsInSource);
    }

    [Fact]
    public async Task DerivedAttribute_ShadowedAppId_DoesNotOverrideGenericPartition()
    {
        const string source = """
                              using DRN.Framework.SharedKernel.Domain;
                              public sealed class CustomAttribute(byte value) : EntityTypeAttribute<DefaultApp>(value)
                              {
                                  public new byte AppId { get; set; }
                              }
                              [Custom(42, AppId = 10)] public class First : SourceKnownEntity;
                              [EntityType<DefaultApp>(42)] public class Second : SourceKnownEntity;
                              """;

        var diagnostics = await RunAnalyzerAsync(source);
        diagnostics.Should().ContainSingle(d => d.Id == "DRNENTITY002");
        diagnostics.Should().HaveCount(1);
        diagnostics[0].GetMessage().Should().Contain("AppId '0'");
    }

    [Fact]
    public async Task PublicEntity_InPrivateContainer_IsIgnoredLocallyAndFromMetadata()
    {
        const string producer = """
                                using DRN.Framework.SharedKernel.Domain;
                                public class Fixture
                                {
                                    private class Container
                                    {
                                        public class Entity : SourceKnownEntity;
                                    }
                                }
                                """;
        const string consumer = """
                                using DRN.Framework.SharedKernel.Domain;
                                [EntityType<DefaultApp>(1)] public class Entity : SourceKnownEntity;
                                """;

        (await RunAnalyzerAsync(producer, consumer)).Should().BeEmpty();
        (await RunAnalyzerWithReferenceAsync(producer, consumer)).Should().BeEmpty();
        // Changing only the container visibility makes the entity require its own attribute.
        var visibleProducer = producer.Replace("private class Container", "public class Container");
        (await RunAnalyzerAsync(visibleProducer, consumer)).Should().ContainSingle(d => d.Id == "DRNENTITY001");
    }

    [Fact]
    public async Task AnnotatedPublicEntity_InPrivateContainer_IsRejectedWithoutCollisions()
    {
        const string producer = """
                                using DRN.Framework.SharedKernel.Domain;
                                public class Fixture
                                {
                                    private class Container
                                    {
                                        [EntityType<DefaultApp>(1)] public class Entity : SourceKnownEntity;
                                    }
                                }
                                """;
        const string consumer = """
                                using DRN.Framework.SharedKernel.Domain;
                                [EntityType<DefaultApp>(1)] public class Entity : SourceKnownEntity;
                                """;

        var diagnostics = await RunAnalyzerAsync(producer, consumer);
        diagnostics.Should().ContainSingle(d => d.Id == "DRNENTITY003");
        diagnostics.Should().HaveCount(1);
        (await RunAnalyzerWithReferenceAsync(producer, consumer)).Should().BeEmpty();
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
        diagnostics.Should().OnlyContain(d => d.Id == "DRNENTITY002" && d.Severity == DiagnosticSeverity.Error);
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
        diagnostics.Should().OnlyContain(d => d.Id == "DRNENTITY004" && d.Severity == DiagnosticSeverity.Warning);
        diagnostics[0].GetMessage().Should().Contain("DomainB.Customer").And.Contain("DomainA.Customer");
        diagnostics[1].GetMessage().Should().Contain("DomainC.Customer").And.Contain("DomainA.Customer");
    }

    [Fact]
    public async Task DuplicateEntityType_AcrossMultipleReferencedAssemblies_ProducesDRNENTITY002AtCompilationEnd()
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
        diagnostics[0].Id.Should().Be("DRNENTITY002");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].Location.Should().Be(Location.None);
        diagnostics[0].GetMessage().Should().Contain("42");
        diagnostics[0].GetMessage().Should().Contain("ReferencedAssemblyA::EntityA");
        diagnostics[0].GetMessage().Should().Contain("ReferencedAssemblyB::EntityB");
    }

    [Fact]
    public async Task Mixed_Collisions_Should_Keep_The_Referenced_Value_And_First_Local_Name_Owners()
    {
        const string producer = """
                                using DRN.Framework.SharedKernel.Domain;
                                namespace External { [EntityType<DefaultApp>(7)] public class Row : SourceKnownEntity; }
                                """;
        const string consumer = """
                                using DRN.Framework.SharedKernel.Domain;
                                namespace First { [EntityType<DefaultApp>(7)] public class Row : SourceKnownEntity; }
                                namespace Second { [EntityType<DefaultApp>(7)] public class Row : SourceKnownEntity; }
                                """;
        var diagnostics = await RunAnalyzerWithReferenceAsync(producer, consumer);
        diagnostics.Should().HaveCount(4);
        diagnostics.Where(d => d.Id == "DRNENTITY002").Should().HaveCount(2).And.OnlyContain(d =>
            d.GetMessage().Contains("ReferencedDomainAssembly::Row") && AnalyzerTestCompilation.LocationText(d) == "EntityType<DefaultApp>(7)");
        var names = diagnostics.Where(d => d.Id == "DRNENTITY004").OrderBy(d => d.Location.SourceSpan.Start).ToArray();
        names.Should().HaveCount(2);
        names[0].GetMessage().Should().Contain("First.Row").And.Contain("External.Row");
        names[1].GetMessage().Should().Contain("Second.Row").And.Contain("First.Row");
    }

    [Fact]
    public async Task Extern_Aliased_Lookalikes_Should_Not_Require_Entity_Metadata()
    {
        var decoy = CSharpCompilation.Create("Lookalikes", [
            CSharpSyntaxTree.ParseText("""
                                       namespace DRN.Framework.SharedKernel.Domain;
                                       public abstract class SourceKnownEntity;
                                       public sealed class EntityTypeAttribute : System.Attribute;
                                       """)
        ], AnalyzerTestCompilation.CoreReferences, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var reference = AnalyzerTestCompilation.EmitReference(decoy).WithAliases(["decoy"]);
        var consumer = CSharpCompilation.Create("Consumer", [
            CSharpSyntaxTree.ParseText("""
                                       extern alias decoy;
                                       using DRN.Framework.SharedKernel.Domain;
                                       using Lookalike = decoy::DRN.Framework.SharedKernel.Domain;
                                       [Lookalike.EntityType] public class Ordinary : Lookalike.SourceKnownEntity;
                                       public class Missing : SourceKnownEntity;
                                       """)
        ], GetBaseReferences().Add(reference), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var diagnostic = (await AnalyzeEntities(consumer, null)).Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNENTITY001");
        AnalyzerTestCompilation.LocationText(diagnostic).Should().Be("Missing");
    }

    [Fact]
    public async Task Mixed_Partial_Entity_Should_Report_An_Invalid_Attribute_At_The_Handwritten_Type()
    {
        var compilation = CSharpCompilation.Create("Mixed", [
            CSharpSyntaxTree.ParseText("""
                                       using DRN.Framework.SharedKernel.Domain;
                                       [EntityType<DefaultApp>(1)] public abstract partial class MixedEntity : SourceKnownEntity;
                                       """, path: "Model.g.cs"),
            CSharpSyntaxTree.ParseText("public abstract partial class MixedEntity;", path: "User.cs")
        ], GetBaseReferences(), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var diagnostic = (await AnalyzeEntities(compilation, null)).Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNENTITY003");
        diagnostic.Location.SourceTree!.FilePath.Should().Be("User.cs");
        AnalyzerTestCompilation.LocationText(diagnostic).Should().Be("MixedEntity");
    }

    [Fact]
    public async Task Entity_Declaration_And_Hidden_Member_Checks_Should_Keep_Their_Own_Partial_Locations()
    {
        var generated = CSharpSyntaxTree.ParseText("""
                                                   using DRN.Framework.SharedKernel.Domain;
                                                   public partial class Missing<T> : SourceKnownEntity;
                                                   public partial class Missing<T> { public new long EntityIdSource; }
                                                   """, path: "Model.g.cs");
        var user = CSharpSyntaxTree.ParseText("""
                                              public partial class Missing<T> { public new long Id; }
                                              public class Container
                                              {
                                                  private class PrivateEntity : DRN.Framework.SharedKernel.Domain.SourceKnownEntity { public new System.Guid EntityId; }
                                              }
                                              """, path: "User.cs");
        var compilation = CSharpCompilation.Create("Mixed", [generated, user], GetBaseReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var diagnostics = await AnalyzeEntities(compilation, null);
        diagnostics.Should().OnlyContain(item => item.Location.SourceTree == user);
        diagnostics.Select(item => (item.Id, AnalyzerTestCompilation.LocationText(item))).Should().BeEquivalentTo(
            [("DRNENTITY001", "Missing"), ("DRNENTITY012", "Missing"), ("DRNENTITY011", "Id"), ("DRNENTITY011", "EntityId")]);
    }

    [Fact]
    public async Task DuplicateEntityName_AcrossMultipleReferencedAssemblies_ProducesDRNENTITY004AtCompilationEnd()
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
        diagnostics[0].Id.Should().Be("DRNENTITY004");
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
    public async Task DuplicateEntityType_WithSameAppId_ProducesDRNENTITY002()
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
        diagnostics[0].Id.Should().Be("DRNENTITY002");
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
    public async Task DuplicateEntityName_WithSameAppId_ProducesDRNENTITY004()
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
        diagnostics[0].Id.Should().Be("DRNENTITY004");
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
    public async Task GenericEntityTypeAttribute_WithSameIAppId_ProducesDRNENTITY002()
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
        diagnostics[0].Id.Should().Be("DRNENTITY002");
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
    public async Task DerivedEntityTypeAttribute_WithSameAppId_ProducesDRNENTITY002()
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
        diagnostics[0].Id.Should().Be("DRNENTITY002");
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
    public async Task ReferencedAssembly_WithSameCustomAppId_ProducesDRNENTITY002()
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
        diagnostics[0].Id.Should().Be("DRNENTITY002");
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
    public async Task ReferencedAssembly_WithDerivedEntityTypeAttribute_ProducesDRNENTITY002_WhenAppIdMatches()
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
        diagnostics[0].Id.Should().Be("DRNENTITY002");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("42");
        diagnostics[0].GetMessage().Should().Contain("5");
    }

    [Fact]
    public async Task MultipleAppIds_InProductionAssembly_ProducesDRNENTITY005()
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
        diagnostics[0].Id.Should().Be("DRNENTITY005");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("TestAssembly").And.Contain("1, 2");
    }

    [Theory]
    [DataInlineUnit("Sample.Test.Unit", "")]
    [DataInlineUnit("ProductionAssembly", "build_property.AllowMultipleAppIds")]
    [DataInlineUnit("CustomTestRunner", "build_property.IsTestProject")]
    [DataInlineUnit("MtpRunnerAssembly", "build_property.UseMicrosoftTestingPlatformRunner")]
    public async Task MultipleAppIds_InExemptTestOrConfiguredAssemblies_ProducesNoDRNENTITY005(string assemblyName, string buildProperty)
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

        var buildProperties = buildProperty.Length == 0
            ? null
            : new Dictionary<string, string> { [buildProperty] = "true" };

        var diagnostics = await RunAnalyzerAsync([testCode], assemblyName: assemblyName, buildProperties: buildProperties);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task TestAppId_WithProductionAppId_ProducesNoDRNENTITY005()
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
    public async Task MultipleAppIds_AcrossReferencedAssembly_ProducesDRNENTITY005()
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
        diagnostics[0].Id.Should().Be("DRNENTITY005");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("ConsumingAssembly").And.Contain("1, 2");
    }

    [Fact]
    public async Task MultipleAppIds_WithTestingReferenceInProductionAssembly_ProducesDRNENTITY005()
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
        diagnostics[0].Id.Should().Be("DRNENTITY005");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("ProductionAssembly").And.Contain("1, 2");
    }

    [Fact]
    public async Task ReferencedAssembly_WithPropertyOnlyIAppId_ProducesDRNENTITY006()
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

        diagnostics.Should().ContainSingle(d => d.Id == "DRNENTITY006" && d.Severity == DiagnosticSeverity.Error);
        diagnostics.First(d => d.Id == "DRNENTITY006").GetMessage().Should().Contain("ExternalEntity");
    }

    [Fact]
    public async Task LocalEntity_WithUnresolvableIAppId_ProducesDRNENTITY006()
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

        diagnostics.Should().ContainSingle(d => d.Id == "DRNENTITY006" && d.Severity == DiagnosticSeverity.Error);
        diagnostics.First(d => d.Id == "DRNENTITY006").GetMessage().Should().Contain("BadEntity");
    }

    [Fact]
    public async Task LocalEntity_WithPropertyOnlyIAppId_ProducesDRNENTITY006()
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

        diagnostics.Should().ContainSingle(d => d.Id == "DRNENTITY006" && d.Severity == DiagnosticSeverity.Error);
        diagnostics.First(d => d.Id == "DRNENTITY006").GetMessage().Should().Contain("BadEntity");
    }

    [Theory]
    [DataInlineUnit("public static byte AppId => 6;")]
    [DataInlineUnit("public static byte AppId { get { if (System.Environment.TickCount == 0) return 6; return Value; } }")]
    [DataInlineUnit("public static byte AppId => Value; static byte IAppId.AppId => 6;")]
    [DataInlineUnit("public static byte AppId { get; set; } = Value;")]
    [DataInlineUnit("public static byte AppId { get; } = Value; static ConflictingApp() { AppId = 6; }")]
    [DataInlineUnit(
        "public static byte AppId => Wrapped; static Wrapper Wrapped => Value; readonly struct Wrapper { public static implicit operator Wrapper(byte x) => default; public static implicit operator byte(Wrapper x) => 6; }")]
    [DataInlineUnit("private static byte _partition = 5; public static byte AppId => _partition;")]
    public async Task LocalEntity_WithUnprovenOrConflictingPartition_ProducesDRNENTITY006(string property)
    {
        var testCode = $$"""
                         using DRN.Framework.SharedKernel.Domain;

                         public readonly struct ConflictingApp : IAppId
                         {
                             public const byte Value = 5;
                             {{property}}
                         }

                         [EntityType<ConflictingApp>(1)]
                         public class BadEntity : SourceKnownEntity;
                         """;

        var diagnostics = await RunAnalyzerAsync(testCode);

        diagnostics.Should().ContainSingle(d => d.Id == "DRNENTITY006" && d.Severity == DiagnosticSeverity.Error);
        diagnostics.First(d => d.Id == "DRNENTITY006").GetMessage().Should().Contain("BadEntity");
    }

    [Theory]
    [InlineData(false, 5, false)]
    [InlineData(false, 6, true)]
    [InlineData(true, 5, false)]
    [InlineData(true, 6, false)]
    public async Task Referenced_Partition_Should_Check_Available_Source_And_Use_The_Published_Constant_In_Metadata(
        bool emitted, int getterValue, bool expectUnresolvable)
    {
        var producer = CSharpCompilation.Create("Partitions", [
            CSharpSyntaxTree.ParseText($$"""
                                         using DRN.Framework.SharedKernel.Domain;
                                         public readonly struct ExternalApp : IAppId
                                         {
                                             public const byte Value = 5;
                                             static byte IAppId.AppId => {{getterValue}};
                                         }
                                         """)
        ], GetBaseReferences(), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        MetadataReference reference = emitted ? AnalyzerTestCompilation.EmitReference(producer) : producer.ToMetadataReference();
        var consumer = CSharpCompilation.Create("Consumer", [
            CSharpSyntaxTree.ParseText("""
                                       using DRN.Framework.SharedKernel.Domain;
                                       [EntityType<ExternalApp>(42)] public class Entity : SourceKnownEntity;
                                       """)
        ], GetBaseReferences().Add(reference), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var diagnostics = await AnalyzeEntities(consumer, null);
        if (expectUnresolvable) diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNENTITY006");
        else diagnostics.Should().BeEmpty();
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
    public async Task Consumer_Defined_AppId_Interface_Should_Not_Create_A_Partition_Obligation()
    {
        var diagnostics = await RunAnalyzerAsync("""
            namespace DRN.Framework.SharedKernel.Domain;
            public interface IAppId { static abstract byte AppId { get; } }
            public readonly struct Local : IAppId { public static byte AppId => 5; }
            """);
        diagnostics.Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit("=> unchecked((byte)(sbyte)Other);", 261, false)]
    [DataInlineUnit("=> (byte)checked((sbyte)Other);", 261, true)]
    [DataInlineUnit("{ get; } = (byte)Other;", 5, true)]
    [DataInlineUnit("{ get; } = Value;", 5, false)]
    public async Task Partition_Producer_Checks_Getter_Without_Entity_Consumers(string getter, int other, bool invalid)
    {
        var diagnostics = await RunAnalyzerAsync($$"""
            using DRN.Framework.SharedKernel.Domain;
            public readonly struct Partition : IAppId
            {
                public const byte Value = 5;
                private static int Other => {{other}};
                public static byte AppId {{getter}}
            }
            """);
        if (invalid) diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNENTITY006");
        else diagnostics.Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit(127, false)]
    [DataInlineUnit(128, true)]
    public async Task Partition_Producer_Checks_Range_Without_Entity_Consumers(int appId, bool outOfRange)
    {
        var diagnostics = await RunAnalyzerAsync($$"""
            using DRN.Framework.SharedKernel.Domain;
            public readonly struct Partition : IAppId
            {
                public const byte Value = {{appId}};
                public static byte AppId => Value;
            }
            """);
        if (outOfRange)
        {
            var diagnostic = diagnostics.Should().ContainSingle().Which;
            diagnostic.Id.Should().Be("DRNENTITY007");
            diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
            diagnostic.GetMessage().Should().Contain("Partition").And.Contain("128");
        }
        else diagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("public const byte AppId = 5; static byte IAppId.AppId => AppId; public static string Value => \"Display label\";")]
    [InlineData("public const byte Value = 5; static byte IAppId.AppId => Value; public static byte AppId => 6;")]
    [InlineData("""
        public const byte AppId = 5;
        static byte IAppId.AppId => AppId;
        private readonly string Value;
        public LabeledApp(string label) => Value = label;
        public string Label => Value;
        """)]
    public async Task LocalEntity_WithUnrelatedValueOrAppIdMember_CompilesCleanly(string members)
    {
        var testCode = $$"""
                         using DRN.Framework.SharedKernel.Domain;

                         public readonly struct LabeledApp : IAppId
                         {
                             {{members}}
                         }

                         [EntityType<LabeledApp>(1)]
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
    public async Task LocalEntity_WithQualifiedConstantProperty_MismatchedConstant_ProducesDRNENTITY006()
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

        diagnostics.Should().ContainSingle(d => d.Id == "DRNENTITY006" && d.Severity == DiagnosticSeverity.Error);
        diagnostics.First(d => d.Id == "DRNENTITY006").GetMessage().Should().Contain("BadEntity");
    }

    [Theory]
    [DataInlineUnit("public static byte AppId => AppId;", TestDisplayName = "Self-recursive AppId")]
    [DataInlineUnit("public static byte AppId => OtherProp; public static byte OtherProp => AppId;", TestDisplayName = "Mutually recursive AppId")]
    public async Task LocalEntity_WithRecursiveAppId_ProducesDRNENTITY006(string getters)
    {
        var testCode = $$"""
            using DRN.Framework.SharedKernel.Domain;

            public readonly struct RecursiveApp : IAppId
            {
                public const byte Value = 5;
                {{getters}}
            }

            [EntityType<RecursiveApp>(1)]
            public class BadEntity : SourceKnownEntity;
            """;
        var diagnostics = await RunAnalyzerAsync(testCode);
        var problem = diagnostics.Should().ContainSingle(d => d.Id == "DRNENTITY006" && d.Severity == DiagnosticSeverity.Error).Which;
        problem.GetMessage().Should().Contain("BadEntity");
    }

    [Fact]
    public async Task LocalEntity_WithNestedIAppId_UsingOuterConstantWithoutOwnConstant_ProducesDRNENTITY006()
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

        diagnostics.Should().ContainSingle(d => d.Id == "DRNENTITY006" && d.Severity == DiagnosticSeverity.Error);
        diagnostics.First(d => d.Id == "DRNENTITY006").GetMessage().Should().Contain("BadEntity");
    }

    [Fact]
    public async Task LocalEntity_WithNonPublicConstant_ProducesDRNENTITY006()
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

        diagnostics.Should().ContainSingle(d => d.Id == "DRNENTITY006" && d.Severity == DiagnosticSeverity.Error);
        diagnostics.First(d => d.Id == "DRNENTITY006").GetMessage().Should().Contain("BadEntity");
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
    public async Task LocalEntity_WithOutOfRangeAppId_ProducesDRNENTITY007()
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

        diagnostics.Should().ContainSingle(d => d.Id == "DRNENTITY007" && d.Severity == DiagnosticSeverity.Error);
        diagnostics.First(d => d.Id == "DRNENTITY007").GetMessage().Should().Contain("BadEntity").And.Contain("128");
    }

    [Fact]
    public async Task ReferencedEntity_WithOutOfRangeAppId_ProducesDRNENTITY007()
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

        diagnostics.Should().ContainSingle(d => d.Id == "DRNENTITY007" && d.Severity == DiagnosticSeverity.Error);
        diagnostics.First(d => d.Id == "DRNENTITY007").GetMessage().Should().Contain("ExternalEntity").And.Contain("255");
    }

    [Fact]
    public async Task MultipleAppIds_AcrossThreeLevelTransitiveAssembly_ProducesDRNENTITY005()
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

        diagnostics.Should().ContainSingle(d => d.Id == "DRNENTITY005" && d.Severity == DiagnosticSeverity.Error);
        diagnostics.First(d => d.Id == "DRNENTITY005").GetMessage().Should().Contain("ProductionAssembly").And.Contain("10, 20");
    }

    [Theory]
    [DataInlineUnit("DomainBaseAssembly", "DomainSubmoduleAssembly", "Microsoft.Consumer", true)]
    [DataInlineUnit("System.DomainBase", "DomainSubmoduleAssembly", "Application", true)]
    [DataInlineUnit("DomainBaseAssembly", "System.DomainSubmodule", "Application", false)]
    [DataInlineUnit("DomainBaseAssembly", "Microsoft.DomainSubmodule", "Application", false)]
    [DataInlineUnit("Microsoft.DomainBase", "Microsoftish.DomainSubmodule", "DRN.FrameworkTools", true)]
    [DataInlineUnit("DomainBaseAssembly", "DomainSubmoduleAssembly", "DRN.Framework.Hosting", false)]
    [DataInlineUnit("DomainBaseAssembly", "DomainSubmoduleAssembly", "DRN.Framework.Customer", true)]
    public async Task TransitiveEntityCollision_UsesTheDeclaringAssemblyToFilterReferences(
        string baseAssembly, string submoduleAssembly, string consumerAssembly, bool expectCollision)
    {
        var referencedSources = new (string AssemblyName, string Source)[]
        {
            (baseAssembly, """
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
            (submoduleAssembly, """
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
            consumingAssemblyName: consumerAssembly);

        if (!expectCollision)
        {
            diagnostics.Should().BeEmpty();
            return;
        }

        diagnostics.Should().ContainSingle();
        diagnostics[0].Id.Should().Be("DRNENTITY002");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain($"{submoduleAssembly}::TransitiveEntity").And.Contain("42");
    }

    [Fact]
    public async Task FrameworkDependencies_AreExcludedWhileFrameworkAndApplicationEntitiesStillCollide()
    {
        var diagnostics = await RunAnalyzerWithMultipleReferencesAsync([
            ("Vendor.Transitive", """
                                  using DRN.Framework.SharedKernel.Domain;
                                  [EntityType<DefaultApp>(7)] public class TransitiveEntity : SourceKnownEntity;
                                  """),
            ("Vendor.Direct", """
                              using DRN.Framework.SharedKernel.Domain;
                              [EntityType<DefaultApp>(7)] public class DirectEntity : TransitiveEntity;
                              """),
            ("DRN.Framework.Utils", """
                                    using DRN.Framework.SharedKernel.Domain;
                                    [EntityType<DefaultApp>(7)] public class FrameworkEntity : DirectEntity;
                                    """),
            ("DRN.Framework.Hosting", "public abstract class FrameworkBase : FrameworkEntity;"),
            ("Vendor.Application", """
                                   using DRN.Framework.SharedKernel.Domain;
                                   [EntityType<DefaultApp>(7)] public class ApplicationEntity : DirectEntity;
                                   """)
        ]);

        diagnostics.Should().ContainSingle();
        diagnostics[0].Id.Should().Be("DRNENTITY002");
        diagnostics[0].GetMessage().Should().Contain("DRN.Framework.Utils::FrameworkEntity")
            .And.Contain("Vendor.Application::ApplicationEntity");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RetargetedCompilationReference_DetectsCollisionWithProducerEntity(bool transitive, bool includeProducerImage)
    {
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
        var producer = CSharpCompilation.Create("Producer.Domain", [CSharpSyntaxTree.ParseText("""
            using DRN.Framework.SharedKernel.Domain;
            [EntityType<DefaultApp>(5)]
            public class ProducerEntity : SourceKnownEntity;
            """)], GetBaseReferences(), options);
        producer.AssertNoCompilerErrors();

        // The consumer binds the same assembly identity to a compatible image with a larger public API.
        var consumerKernel = CreateSharedKernelReference(SharedKernelDomainStubs + "\npublic class NewKernelApi;");
        var directProducer = producer;
        var directTypeName = "ProducerEntity";
        if (transitive)
        {
            directTypeName = "IntermediateEntity";
            directProducer = CSharpCompilation.Create("Intermediate.Domain", [CSharpSyntaxTree.ParseText("""
                using DRN.Framework.SharedKernel.Domain;
                public abstract class IntermediateEntity : SourceKnownEntity;
                """)], GetBaseReferences().Add(producer.ToMetadataReference()), options);
            directProducer.AssertNoCompilerErrors();
        }

        var project = directProducer.ToMetadataReference();
        var references = AnalyzerTestCompilation.CoreReferences.Add(AnalyzerTestCompilation.ConsoleReference)
            .Add(consumerKernel).Add(project);
        if (includeProducerImage) references = references.Add(AnalyzerTestCompilation.EmitReference(producer));
        var consumer = CSharpCompilation.Create("Consumer.Domain", [CSharpSyntaxTree.ParseText("""
            using DRN.Framework.SharedKernel.Domain;
            [EntityType<DefaultApp>(5)]
            public class ConsumerEntity : SourceKnownEntity;
            """)], references, options);

        var producerBase = producer.GetTypeByMetadataName("ProducerEntity")!.BaseType!;
        var boundProject = (IAssemblySymbol)consumer.GetAssemblyOrModuleSymbol(project)!;
        var retargetedBase = boundProject.GetTypeByMetadataName(directTypeName)!.BaseType!;
        var consumerBase = consumer.GetTypeByMetadataName("ConsumerEntity")!.BaseType!;
        SymbolEqualityComparer.Default.Equals(retargetedBase, producerBase).Should().BeFalse();
        SymbolEqualityComparer.Default.Equals(retargetedBase, consumerBase).Should().BeTrue();

        var collision = (await AnalyzeEntities(consumer, null)).Should().ContainSingle().Which;
        collision.Id.Should().Be("DRNENTITY002");
        collision.Severity.Should().Be(DiagnosticSeverity.Error);
        collision.GetMessage().Should().Contain("Producer.Domain::ProducerEntity").And.Contain("ConsumerEntity");
    }

    [Fact]
    public async Task ThreeLevelTransitiveCompilationReference_ResolvesAppIdAndDetectsCollisions()
    {
        var baseReferences = GetBaseReferences();
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

        var appCompilation = CSharpCompilation.Create(
            "AppPartitionCompilation",
            [
                CSharpSyntaxTree.ParseText("""
                                           using DRN.Framework.SharedKernel.Domain;

                                           namespace AppPartition
                                           {
                                               public readonly struct SharedApp : IAppId
                                               {
                                                   public const byte Value = 42;
                                                   public static byte AppId => Value;
                                               }

                                               public sealed class SharedEntityTypeAttribute(byte entityType) : EntityTypeAttribute<SharedApp>(entityType);
                                           }
                                           """, path: "AppPartition.cs")
            ],
            baseReferences,
            options);

        var domainCompilation = CSharpCompilation.Create(
            "DomainCompilation",
            [
                CSharpSyntaxTree.ParseText("""
                                           using DRN.Framework.SharedKernel.Domain;

                                           namespace TransitiveDomain
                                           {
                                               [AppPartition.SharedEntityType(5)]
                                               public class TransitiveEntity : SourceKnownEntity;
                                           }
                                           """, path: "DomainCompilation.cs")
            ],
            baseReferences.Add(appCompilation.ToMetadataReference()),
            options);

        var middleCompilation = CSharpCompilation.Create(
            "MiddleCompilation",
            [
                CSharpSyntaxTree.ParseText("""
                                           namespace MiddleLayer
                                           {
                                               public class MiddleService { public int Ping() => 1; }
                                           }
                                           """, path: "MiddleCompilation.cs")
            ],
            baseReferences.Add(domainCompilation.ToMetadataReference()),
            options);

        var consumerCompilation = CSharpCompilation.Create(
            "LeafConsumerCompilation",
            [
                CSharpSyntaxTree.ParseText("""
                                           using DRN.Framework.SharedKernel.Domain;

                                           namespace LeafConsumer
                                           {
                                               public readonly struct LocalApp : IAppId
                                               {
                                                   public const byte Value = 42;
                                                   public static byte AppId => Value;
                                               }

                                               [EntityType<LocalApp>(5)]
                                               public class CollidingEntity : SourceKnownEntity;
                                           }
                                           """, path: "LeafConsumer.cs")
            ],
            baseReferences.Add(middleCompilation.ToMetadataReference()),
            options);

        appCompilation.AssertNoCompilerErrors();
        domainCompilation.AssertNoCompilerErrors();
        middleCompilation.AssertNoCompilerErrors();

        var diagnostics = await AnalyzeEntities(consumerCompilation, null);
        diagnostics.Should().ContainSingle(d => d.Id == "DRNENTITY002" && d.Severity == DiagnosticSeverity.Error);
        diagnostics.Should().NotContain(d => d.Id == "DRNENTITY006");
        diagnostics.Single().GetMessage().Should().Contain("5").And.Contain("42").And.Contain("TransitiveEntity");
    }

    [Fact]
    public async Task PreCanceled_Token_Should_Cancel_EntityType_Analysis()
    {
        var compilation = CSharpCompilation.Create(
            "CanceledEntityAssembly",
            [
                CSharpSyntaxTree.ParseText("""
                                           using DRN.Framework.SharedKernel.Domain;

                                           [EntityType<DefaultApp>(1)]
                                           public class Order : SourceKnownEntity;
                                           """, path: "CanceledOrder.cs")
            ],
            GetBaseReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.AssertNoCompilerErrors();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var act = async () => await compilation.WithAnalyzers([new SourceKnownEntityTypeAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(cancellation.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
