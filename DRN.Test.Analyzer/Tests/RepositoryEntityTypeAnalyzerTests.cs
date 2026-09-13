namespace DRN.Test.Analyzer.Tests;

public partial class SourceKnownEntityTypeAnalyzerTests
{
    private const string RepositoryTypeStubs = """
        using DRN.Framework.SharedKernel.Domain;
        namespace DRN.Framework.SharedKernel.Domain.Repository
        {
            public interface ISourceKnownRepository<TEntity> where TEntity : AggregateRoot;
        }
        namespace DRN.Framework.EntityFramework.Domain
        {
            public abstract class SourceKnownRepository<TContext, TEntity>
                : DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<TEntity>
                where TEntity : AggregateRoot;
        }
        """;

    [Fact]
    public async Task RepositoryBindings_RejectAbstractEntitiesInDeclarationsAndTypeUsages()
    {
        const string source = """
            using DRN.Framework.SharedKernel.Domain;
            using DRN.Framework.SharedKernel.Domain.Repository;
            using DRN.Framework.EntityFramework.Domain;
            public abstract class AbstractOrder : AggregateRoot;
            public interface InvalidContract : ISourceKnownRepository<AggregateRoot>;
            public abstract class InvalidBase : SourceKnownRepository<object, AbstractOrder>;
            public class Consumer
            {
                public ISourceKnownRepository<AggregateRoot> Field;
                public ISourceKnownRepository<AbstractOrder> Property { get; set; }
                public ISourceKnownRepository<AggregateRoot> Get(ISourceKnownRepository<AbstractOrder> value) => null;
                public object Inspect() => typeof(ISourceKnownRepository<AggregateRoot>);
            }
            """;

        var diagnostics = await RunAnalyzerAsync(RepositoryTypeStubs, source);
        diagnostics.Should().HaveCount(7);
        diagnostics.Should().OnlyContain(d => d.Id == "DRN0013" && d.Severity == DiagnosticSeverity.Error);
        diagnostics.Should().OnlyContain(d => d.Descriptor == DiagnosticDescriptors.AbstractEntityMetadataArgument);
        diagnostics.Select(d => d.GetMessage()).Should().Contain(
            "Abstract entity 'DRN.Framework.SharedKernel.Domain.AggregateRoot' cannot be used with 'DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<DRN.Framework.SharedKernel.Domain.AggregateRoot>'; a concrete entity type is required");
        diagnostics.Select(d => source.Substring(d.Location.SourceSpan.Start, d.Location.SourceSpan.Length))
            .Should().OnlyContain(text => text == "AggregateRoot" || text == "AbstractOrder");
    }

    [Fact]
    public async Task DerivedRepositoryBindings_ResolveEntityArgumentsAcrossMetadata()
    {
        const string referenced = """
            namespace Library
            {
                public interface IRepository<T> : DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<T>
                    where T : DRN.Framework.SharedKernel.Domain.AggregateRoot;
                public abstract class Repository<T> : DRN.Framework.EntityFramework.Domain.SourceKnownRepository<object, T>
                    where T : DRN.Framework.SharedKernel.Domain.AggregateRoot;
            }
            """;
        const string consuming = """
            using DRN.Framework.SharedKernel.Domain;
            public class Consumer
            {
                public Library.IRepository<AggregateRoot> Field;
                public Library.Repository<AggregateRoot> Property { get; set; }
            }
            """;

        var diagnostics = await RunAnalyzerWithReferenceAsync(RepositoryTypeStubs + referenced, consuming);
        diagnostics.Should().HaveCount(2);
        diagnostics.Should().OnlyContain(d => d.Id == "DRN0013");
    }

    [Fact]
    public async Task RepositoryAliases_CannotHideInvalidBindings()
    {
        const string source = """
            using Invalid = DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<DRN.Framework.SharedKernel.Domain.AggregateRoot>;
            public class Consumer { public Invalid Field; }
            """;

        var diagnostics = await RunAnalyzerAsync(RepositoryTypeStubs, source);
        diagnostics.Should().HaveCount(2);
        diagnostics.Should().OnlyContain(d => d.Id == "DRN0013");
    }

    [Fact]
    public async Task RepositoryBindings_AllowConcreteEntitiesGenericForwardingAndUnrelatedTypes()
    {
        const string source = """
            using DRN.Framework.SharedKernel.Domain;
            using DRN.Framework.SharedKernel.Domain.Repository;
            using DRN.Framework.EntityFramework.Domain;
            [EntityType<DefaultApp>(1)] public class Order(long id) : AggregateRoot(id);
            public abstract class RepositoryBase<T> : SourceKnownRepository<object, T> where T : AggregateRoot;
            public interface IRepository<T> : ISourceKnownRepository<T> where T : AggregateRoot;
            public class Orders : RepositoryBase<Order>;
            public class Consumer<T> where T : AggregateRoot
            {
                public ISourceKnownRepository<T> Generic;
                public ISourceKnownRepository<Order> Concrete;
                public IRepository<Order> Derived;
                public object Open() => typeof(ISourceKnownRepository<>);
                public Unrelated<AggregateRoot> Other;
            }
            public class Unrelated<T>;
            """;

        (await RunAnalyzerAsync(RepositoryTypeStubs, source)).Should().BeEmpty();
    }

    [Fact]
    public async Task EntityMetadataMethodGroups_RejectAbstractEntitiesButAllowRuntimeInstanceOverloads()
    {
        const string source = """
            using System;
            using DRN.Framework.SharedKernel.Domain;
            public class Consumer
            {
                public void Run(SourceKnownEntityId id)
                {
                    Func<EntityTypeId> invalid = SourceKnownEntity.GetEntityTypeId<AggregateRoot>;
                    Action invalidValidation = id.Validate<AggregateRoot>;
                    Func<AggregateRoot, EntityTypeId> valid = SourceKnownEntity.GetEntityTypeId<AggregateRoot>;
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source);
        diagnostics.Should().HaveCount(2);
        diagnostics.Should().OnlyContain(d => d.Id == "DRN0013");
    }

    [Fact]
    public async Task UtilityAndEntityIdMethodGroups_RequireConcreteMetadataTypes()
    {
        const string source = """
            using System;
            using DRN.Framework.SharedKernel.Domain;
            [EntityType<DefaultApp>(1)] public class Order : AggregateRoot;
            public class Consumer
            {
                public void Run<T>(ISourceKnownEntityIdOperations ops, SourceKnownEntity entity,
                    DRN.Framework.Utils.Ids.ISourceKnownEntityIdUtils ids,
                    DRN.Framework.Utils.Ids.ISourceKnownIdUtils numeric) where T : SourceKnownEntity
                {
                    Func<Guid, SourceKnownEntityIdFormat, SourceKnownEntityId> invalid = ops.Validate<AggregateRoot>;
                    Func<Guid, SourceKnownEntityIdFormat, SourceKnownEntityId> invalidEntity = entity.GetEntityId<AggregateRoot>;
                    Func<SourceKnownEntityId> invalidGeneration = ids.Generate<AggregateRoot>;
                    Func<long> invalidNumeric = numeric.Next<AggregateRoot>;
                    Func<Guid, SourceKnownEntityIdFormat, SourceKnownEntityId> valid = ops.Validate<Order>;
                    Func<Guid, SourceKnownEntityIdFormat, SourceKnownEntityId> generic = entity.GetEntityId<T>;
                    Func<SourceKnownEntityId> validGeneration = ids.Generate<Order>;
                }
            }
            namespace DRN.Framework.Utils.Ids
            {
                public interface ISourceKnownEntityIdUtils : ISourceKnownEntityIdOperations
                {
                    SourceKnownEntityId Generate<TEntity>() where TEntity : SourceKnownEntity;
                }
                public interface ISourceKnownIdUtils
                {
                    long Next<TEntity>() where TEntity : SourceKnownEntity;
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source);
        diagnostics.Should().HaveCount(4);
        diagnostics.Should().OnlyContain(d => d.Id == "DRN0013");
    }
}
