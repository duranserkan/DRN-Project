namespace DRN.Test.Analyzer.Tests;

public partial class SourceKnownEntityTypeAnalyzerTests
{
    [Fact]
    public async Task ConstantIdentityArguments_ReportInvalidValuesAtTheirExpressions()
    {
        const string source = """
            using System;
            using DRN.Framework.SharedKernel.Domain;
            public class Consumer
            {
                public void Run(ISourceKnownEntityIdOperations ops)
                {
                    const byte InvalidApp = 128;
                    var first = new EntityTypeId(AppId: InvalidApp, EntityType: 7);
                    EntityTypeId second = new(7, 255);
                    var third = first with { AppId = 200 };
                    var fourth = new EntityTypeId { AppId = 201 };
                    ops.Parse(format: (SourceKnownEntityIdFormat)99, id: Guid.Empty);
                    ops.Parse(Guid.Empty, (SourceKnownEntityIdFormat)(-1));
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source);
        diagnostics.Should().HaveCount(6);
        diagnostics.Count(d => d.Id == "DRN0009").Should().Be(4);
        diagnostics.Count(d => d.Id == "DRN0010").Should().Be(2);
        diagnostics.Should().OnlyContain(d => d.Severity == DiagnosticSeverity.Error);
        diagnostics.Select(d => source.Substring(d.Location.SourceSpan.Start, d.Location.SourceSpan.Length))
            .Should().BeEquivalentTo("InvalidApp", "255", "200", "201", "(SourceKnownEntityIdFormat)99", "(SourceKnownEntityIdFormat)(-1)");
    }

    [Fact]
    public async Task ValidBoundariesAndDynamicArguments_ProduceNoDiagnostics()
    {
        const string source = """
            using System;
            using DRN.Framework.SharedKernel.Domain;
            public class Consumer
            {
                public void Run(ISourceKnownEntityIdOperations ops, byte appId, SourceKnownEntityIdFormat format)
                {
                    _ = new EntityTypeId(0, 0);
                    _ = new EntityTypeId(255, 127);
                    var identity = new EntityTypeId(7, appId);
                    _ = identity with { AppId = appId };
                    ops.Parse(Guid.Empty);
                    ops.Parse(Guid.Empty, SourceKnownEntityIdFormat.ConfiguredDefault);
                    ops.Parse(Guid.Empty, SourceKnownEntityIdFormat.Secure);
                    ops.Parse(Guid.Empty, SourceKnownEntityIdFormat.Plain);
                    ops.Parse(Guid.Empty, SourceKnownEntityIdFormat.Auto);
                    ops.Parse(Guid.Empty, format);
                }
            }
            namespace Unrelated
            {
                public record EntityTypeId(byte EntityType, byte AppId);
                public class Consumer { public object Run() => new EntityTypeId(7, 200); }
            }
            """;

        (await RunAnalyzerAsync(source)).Should().BeEmpty();
    }

    [Fact]
    public async Task FormatAssignments_CheckFrameworkEnumButNotUnrelatedEnums()
    {
        const string source = """
            using DRN.Framework.SharedKernel.Domain;
            public class Consumer
            {
                public SourceKnownEntityIdFormat Format { get; set; }
                public void Run(SourceKnownEntityIdFormat dynamicFormat)
                {
                    Format = (SourceKnownEntityIdFormat)4;
                    Format = dynamicFormat;
                    var other = new Consumer { Format = (SourceKnownEntityIdFormat)5 };
                }
            }
            namespace Unrelated
            {
                public enum SourceKnownEntityIdFormat { Anything }
                public class Consumer
                {
                    public SourceKnownEntityIdFormat Format { get; set; }
                    public void Run() => Format = (SourceKnownEntityIdFormat)99;
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source);
        diagnostics.Should().HaveCount(2);
        diagnostics.Should().OnlyContain(d => d.Id == "DRN0010");
    }

    [Fact]
    public async Task PartialEntity_ReportsHiddenMemberAndGenericShapeOnce()
    {
        const string first = """
            using DRN.Framework.SharedKernel.Domain;
            [EntityType<DefaultApp>(1)] public partial class Entity<T> : AggregateRoot;
            """;
        const string second = """
            public partial class Entity<T> { public new long Id { get; set; } }
            """;

        var diagnostics = await RunAnalyzerAsync(first, second);
        diagnostics.Select(d => d.Id).Should().BeEquivalentTo("DRN0011", "DRN0012");
    }

    [Fact]
    public async Task HiddenIdentityMembers_ReportExplicitImplicitAndDifferentMemberKinds()
    {
        const string source = """
            using System;
            using DRN.Framework.SharedKernel.Domain;
            public abstract class Hidden : AggregateRoot
            {
                public new long Id { get; set; }
                public Guid EntityId;
                public new void EntityIdSource() { }
            }
            public abstract class Implicit : AggregateRoot
            {
                public long Id { get; set; }
            }
            public class Unrelated
            {
                public long Id { get; set; }
                public Guid EntityId { get; set; }
            }
            public abstract class Safe : AggregateRoot
            {
                public long Read() => Id;
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source);
        diagnostics.Should().HaveCount(4);
        diagnostics.Should().OnlyContain(d => d.Id == "DRN0011" && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task GenericEntities_IncludeGenericContainersButAllowAbstractAndClosedBases()
    {
        const string source = """
            using DRN.Framework.SharedKernel.Domain;
            [EntityType<DefaultApp>(1)] public class Item<T> : SourceKnownEntity;
            public class Container<T>
            {
                [EntityType<DefaultApp>(2)] public class Nested : SourceKnownEntity;
            }
            public abstract class Base<T> : SourceKnownEntity;
            [EntityType<DefaultApp>(3)] public class TextItem : Base<string>;
            [EntityType<DefaultApp>(4)] public class NumberItem : Base<int>;
            public class PrivateContainer
            {
                private class Ignored<T> : SourceKnownEntity;
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source);
        diagnostics.Should().HaveCount(2);
        diagnostics.Should().OnlyContain(d => d.Id == "DRN0012" && d.Severity == DiagnosticSeverity.Warning);
        diagnostics.Select(d => d.GetMessage()).Should().Contain(m => m.Contains("Container<T>.Nested"));
    }

    [Fact]
    public async Task AbstractMetadataArguments_ReportFrameworkLookupsIncludingReferencedBaseTypes()
    {
        const string source = """
            using System;
            using DRN.Framework.SharedKernel.Domain;
            public class Consumer
            {
                public void Run(ISourceKnownEntityIdOperations ops, SourceKnownEntity entity, SourceKnownEntityId id)
                {
                    SourceKnownEntity.GetEntityTypeId<AggregateRoot>();
                    SourceKnownEntity.GetEntityType<SourceKnownEntity>();
                    SourceKnownEntity.GetAppId<AggregateRoot>();
                    SourceKnownEntity.GetEntityTypeId(typeof(AggregateRoot));
                    EntityTypeRegistry.GetEntityTypeId(typeof(SourceKnownEntity));
                    ops.Validate<AggregateRoot>(Guid.Empty);
                    entity.GetEntityId<AggregateRoot>(Guid.Empty);
                    id.Validate<AggregateRoot>();
                    id.HasSameEntityType<AggregateRoot>();
                    id.HasSameEntityTypeId<AggregateRoot>();
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source);
        diagnostics.Should().HaveCount(10);
        diagnostics.Should().OnlyContain(d => d.Id == "DRN0013" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task RuntimeEntityOverloadsAndGenericForwarding_ProduceNoDiagnostics()
    {
        const string source = """
            using System;
            using DRN.Framework.SharedKernel.Domain;
            [EntityType<DefaultApp>(1)] public class Order : AggregateRoot;
            public abstract class AppEntity : AggregateRoot, IAppId
            {
                public const byte Value = 0;
                public static byte AppId => Value;
            }
            public class Consumer
            {
                public void Run<T>(ISourceKnownEntityIdOperations ops, AggregateRoot instance) where T : SourceKnownEntity
                {
                    SourceKnownEntity.GetEntityTypeId<Order>();
                    SourceKnownEntity.GetEntityTypeId<T>();
                    SourceKnownEntity.GetEntityTypeId<AggregateRoot>(instance);
                    SourceKnownEntity.GetEntityType<AggregateRoot>(instance);
                    SourceKnownEntity.GetAppId<AggregateRoot>(instance);
                    SourceKnownEntity.GetEntityTypeId(typeof(Order));
                    SourceKnownEntity.GetEntityTypeId(instance.GetType());
                    ops.Validate<Order>(Guid.Empty);
                    ops.Validate<T>(Guid.Empty);
                    ops.Validate<AppEntity>(Guid.Empty, entityType: 1);
                    Unrelated.GetEntityTypeId<AggregateRoot>();
                }
            }
            public static class Unrelated { public static void GetEntityTypeId<T>() { } }
            """;

        (await RunAnalyzerAsync(source)).Should().BeEmpty();
    }

    [Fact]
    public async Task ReferencedUtilityAndRepositoryApis_RejectAbstractEntityArguments()
    {
        const string referenced = """
            using System;
            using DRN.Framework.SharedKernel.Domain;
            namespace DRN.Framework.Utils.Ids
            {
                public interface ISourceKnownEntityIdUtils : ISourceKnownEntityIdOperations
                {
                    SourceKnownEntityId Generate<TEntity>() where TEntity : SourceKnownEntity;
                    SourceKnownEntityId GeneratePlain<TEntity>() where TEntity : SourceKnownEntity;
                    SourceKnownEntityId GenerateSecure<TEntity>() where TEntity : SourceKnownEntity;
                }
                public class SourceKnownEntityIdUtils
                {
                    public SourceKnownEntityId Generate<TEntity>() where TEntity : SourceKnownEntity => default;
                    public SourceKnownEntityId GeneratePlain<TEntity>() where TEntity : SourceKnownEntity => default;
                    public SourceKnownEntityId GenerateSecure<TEntity>() where TEntity : SourceKnownEntity => default;
                    public SourceKnownEntityId Validate<TEntity>(Guid id) where TEntity : SourceKnownEntity => default;
                }
                public interface ISourceKnownIdUtils { long Next<TEntity>() where TEntity : SourceKnownEntity; }
                public class SourceKnownIdUtils { public long Next<TEntity>() where TEntity : SourceKnownEntity => 0; }
            }
            namespace DRN.Framework.SharedKernel.Domain.Repository
            {
                public interface ISourceKnownRepository<TEntity> where TEntity : AggregateRoot
                {
                    SourceKnownEntityId GetEntityId<TOtherEntity>(Guid id) where TOtherEntity : SourceKnownEntity;
                }
            }
            namespace DRN.Framework.EntityFramework.Domain
            {
                public abstract class SourceKnownRepository<TContext, TEntity> where TEntity : AggregateRoot
                {
                    public SourceKnownEntityId GetEntityId<TOtherEntity>(Guid id) where TOtherEntity : SourceKnownEntity => default;
                }
            }
            """;
        const string consuming = """
            using System;
            using DRN.Framework.SharedKernel.Domain;
            using DRN.Framework.SharedKernel.Domain.Repository;
            using DRN.Framework.Utils.Ids;
            using DRN.Framework.EntityFramework.Domain;
            [EntityType<DefaultApp>(1)] public class Order : AggregateRoot;
            public class Consumer
            {
                public void Run(ISourceKnownEntityIdUtils ids, SourceKnownEntityIdUtils implementation,
                    ISourceKnownIdUtils numeric, SourceKnownIdUtils numericImplementation,
                    ISourceKnownRepository<Order> repository, SourceKnownRepository<object, Order> repositoryImplementation)
                {
                    ids.Generate<AggregateRoot>();
                    ids.GeneratePlain<AggregateRoot>();
                    ids.GenerateSecure<AggregateRoot>();
                    ids.Validate<AggregateRoot>(Guid.Empty);
                    implementation.Generate<AggregateRoot>();
                    implementation.GeneratePlain<AggregateRoot>();
                    implementation.GenerateSecure<AggregateRoot>();
                    implementation.Validate<AggregateRoot>(Guid.Empty);
                    numeric.Next<AggregateRoot>();
                    numericImplementation.Next<AggregateRoot>();
                    repository.GetEntityId<AggregateRoot>(Guid.Empty);
                    repositoryImplementation.GetEntityId<AggregateRoot>(Guid.Empty);
                    ids.Generate<Order>();
                    numeric.Next<Order>();
                    repository.GetEntityId<Order>(Guid.Empty);
                }
            }
            """;

        var diagnostics = await RunAnalyzerWithReferenceAsync(referenced, consuming);
        diagnostics.Should().HaveCount(12);
        diagnostics.Should().OnlyContain(d => d.Id == "DRN0013");
    }

    [Fact]
    public void UsageDiagnostics_HavePublishedSeverityAndHelpLinks()
    {
        var diagnostics = new SourceKnownEntityTypeAnalyzer().SupportedDiagnostics
            .Where(d => d.Id is "DRN0009" or "DRN0010" or "DRN0011" or "DRN0012" or "DRN0013" or "DRN0014").ToArray();
        diagnostics.Should().HaveCount(6);
        diagnostics.Should().OnlyContain(d => d.IsEnabledByDefault && d.HelpLinkUri == DiagnosticDescriptors.HelpLinkUri);
        diagnostics.Count(d => d.DefaultSeverity == DiagnosticSeverity.Error).Should().Be(4);
        diagnostics.Count(d => d.DefaultSeverity == DiagnosticSeverity.Warning).Should().Be(2);
        DiagnosticDescriptors.AbstractEntityMetadataArgument.Title.ToString().Should().Be("Concrete entity type required");
        new SourceKnownEntityTypeAnalyzer().SupportedDiagnostics.Select(d => d.Id).Should().OnlyHaveUniqueItems();
    }
}
