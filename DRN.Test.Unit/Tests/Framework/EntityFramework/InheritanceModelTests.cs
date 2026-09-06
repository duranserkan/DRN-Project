using System.Reflection;
using DRN.Framework.EntityFramework.Context;
using DRN.Framework.EntityFramework.Context.Interceptors;
using DRN.Framework.EntityFramework.Extensions;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DRN.Test.Unit.Tests.Framework.EntityFramework;

public class InheritanceModelTests
{
    [Theory]
    [DataInlineUnit("TPH")]
    [DataInlineUnit("TPT")]
    [DataInlineUnit("TPC")]
    public void Conventions_Should_Preserve_Hierarchy_Keys_And_Concrete_Entity_Metadata(string strategy)
    {
        var options = new DbContextOptionsBuilder().UseNpgsql("Host=localhost;Database=test").Options;
        using InheritanceContext context = strategy switch
        {
            "TPH" => new TphContext(options),
            "TPT" => new TptContext(options),
            "TPC" => new TpcContext(options),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy))
        };

        var root = context.Model.FindEntityType(typeof(InheritanceRoot))!;
        root.BaseType.Should().BeNull();
        root.GetMappingStrategy().Should().Be(strategy);
        var key = root.FindPrimaryKey()!;
        key.Properties.Should().ContainSingle().Which.Name.Should().Be(nameof(SourceKnownEntity.Id));

        foreach (var type in new[] { typeof(InheritanceBranch), typeof(InheritanceLeaf) })
        {
            var mapped = context.Model.FindEntityType(type)!;
            mapped.BaseType.Should().NotBeNull();
            mapped.FindPrimaryKey().Should().BeSameAs(key);
            mapped.FindProperty(nameof(SourceKnownEntity.Id))!.GetValueGeneratorFactory()!(key.Properties[0], mapped)
                .Should().BeOfType<SourceKnownIdValueGenerator>();
            mapped.FindProperty(nameof(SourceKnownEntity.ExtendedProperties))!.GetColumnType().Should().Be("jsonb");
            mapped.FindProperty(nameof(SourceKnownEntity.EntityId)).Should().BeNull();
            mapped.FindProperty(nameof(SourceKnownEntity.EntityIdSource)).Should().BeNull();
        }

        context.Model.FindEntityType(typeof(InheritanceLeaf))!
            .FindProperty(nameof(InheritanceLeaf.Description)).Should().NotBeNull();
        var standalone = context.Model.FindEntityType(typeof(StandaloneEntity))!;
        standalone.BaseType.Should().BeNull();
        standalone.FindPrimaryKey()!.Properties.Should().ContainSingle().Which.Name.Should().Be(nameof(SourceKnownEntity.Id));
        standalone.FindProperty(nameof(SourceKnownEntity.Id))!.GetValueGeneratorFactory()!(standalone.FindProperty(nameof(SourceKnownEntity.Id))!, standalone)
            .Should().BeOfType<SourceKnownIdValueGenerator>();

        Type[] expected = [typeof(InheritanceBranch), typeof(InheritanceLeaf), typeof(StandaloneEntity)];
        DrnContextServiceRegistrationAttribute.GetModelDomainEntityTypes(context).Should().BeEquivalentTo(expected);
        DrnContextServiceRegistrationAttribute.GetHostDomainEntityTypes(null, [context]).Should().BeEquivalentTo(expected);
        var validate = () => DrnContextServiceRegistrationAttribute.ValidateEntityTypes(context, scopedLog: null);
        validate.Should().NotThrow();

        InheritanceRoot entity = new InheritanceLeaf();
        SourceKnownEntity.GetEntityTypeId(entity).Should().Be(new EntityTypeId(212, TestApp.AppId));
        SourceKnownEntity.GetEntityTypeId<InheritanceBranch>().Should().Be(new EntityTypeId(211, TestApp.AppId));
    }

    [Fact]
    public void Validation_Should_Still_Reject_Concrete_Entities_Without_Metadata()
    {
        var options = new DbContextOptionsBuilder().UseNpgsql("Host=localhost;Database=test").Options;
        using var context = new MissingMetadataContext(options);

        DrnContextServiceRegistrationAttribute.GetModelDomainEntityTypes(context)
            .Should().BeEquivalentTo([typeof(MissingMetadataEntity)]);
        var validate = () => DrnContextServiceRegistrationAttribute.ValidateEntityTypes(context, scopedLog: null);
        validate.Should().ThrowExactly<UnprocessableEntityException>()
            .WithMessage($"*{nameof(MissingMetadataEntity)}*");
    }

    [Fact]
    public void Conventions_Should_Configure_Explicitly_Mapped_SourceKnownEntity_Root()
    {
        var options = new DbContextOptionsBuilder().UseNpgsql("Host=localhost;Database=test").Options;
        using var context = new SourceKnownRootContext(options);

        var root = context.Model.FindEntityType(typeof(SourceKnownEntity))!;
        var entity = context.Model.FindEntityType(typeof(StandaloneEntity))!;
        entity.BaseType.Should().BeSameAs(root);
        entity.FindPrimaryKey().Should().BeSameAs(root.FindPrimaryKey());
        entity.FindProperty(nameof(SourceKnownEntity.Id))!.GetValueGeneratorFactory()!(root.FindProperty(nameof(SourceKnownEntity.Id))!, entity)
            .Should().BeOfType<SourceKnownIdValueGenerator>();
        DrnContextServiceRegistrationAttribute.GetModelDomainEntityTypes(context)
            .Should().BeEquivalentTo([typeof(StandaloneEntity)]);
        var validate = () => DrnContextServiceRegistrationAttribute.ValidateEntityTypes(context, scopedLog: null);
        validate.Should().NotThrow();
    }

    [Fact]
    public void Host_Discovery_Should_Ignore_Private_Containers_And_Retain_Visible_Metadata_Validation()
    {
        var assembly = Substitute.For<Assembly>();
        assembly.GetName().Returns(new AssemblyName("PrivacyDomain"));
        assembly.GetTypes().Returns([
            typeof(StandaloneEntity), typeof(MissingMetadataEntity), typeof(PrivateHelperEntity),
            typeof(PrivateContainer.NestedEntity), typeof(PrivateContainer.PublicContainer.DeeplyNestedEntity)
        ]);
        var container = new DrnServiceContainer(assembly, lifetimeAttributes: [], serviceRegistrationTypes: []);
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(IEnumerable<DrnServiceContainer>)).Returns(new[] { container });

        var entities = DrnContextServiceRegistrationAttribute.GetHostDomainEntityTypes(provider, []);

        entities.Should().BeEquivalentTo([typeof(StandaloneEntity), typeof(MissingMetadataEntity)]);
        var validation = DrnContextServiceRegistrationAttribute.GetEntityTypeValidationResult(entities);
        validation.MissingEntityTypes.Should().Equal(typeof(MissingMetadataEntity).FullName!);
        var validate = () => DrnContextServiceRegistrationHelper.ValidateHostEntityTypes(validation, null);
        validate.Should().ThrowExactly<UnprocessableEntityException>()
            .WithMessage($"*{nameof(MissingMetadataEntity)}*");
    }

    public abstract class InheritanceRoot : SourceKnownEntity;

    [EntityType<TestApp>(211)]
    public class InheritanceBranch : InheritanceRoot;

    [EntityType<TestApp>(212)]
    public sealed class InheritanceLeaf : InheritanceBranch
    {
        public string Description { get; set; } = string.Empty;
    }

    [EntityType<TestApp>(213)]
    public sealed class StandaloneEntity : SourceKnownEntity;

    private sealed class PrivateHelperEntity : SourceKnownEntity;

    private static class PrivateContainer
    {
        public sealed class NestedEntity : SourceKnownEntity;

        public static class PublicContainer
        {
            public sealed class DeeplyNestedEntity : SourceKnownEntity;
        }
    }

#pragma warning disable DRN0001 // Missing metadata intentionally exercises runtime validation.
    public sealed class MissingMetadataEntity : InheritanceRoot;
#pragma warning restore DRN0001

    private abstract class InheritanceContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InheritanceRoot>();
            modelBuilder.Entity<InheritanceBranch>();
            modelBuilder.Entity<InheritanceLeaf>();
            modelBuilder.Entity<StandaloneEntity>();
            modelBuilder.Entity<PrivateHelperEntity>();
            modelBuilder.Entity<PrivateContainer.NestedEntity>();
            modelBuilder.Entity<PrivateContainer.PublicContainer.DeeplyNestedEntity>();
            ConfigureStrategy(modelBuilder);
            this.ModelCreatingDefaults(modelBuilder);
        }

        protected abstract void ConfigureStrategy(ModelBuilder modelBuilder);
    }

    private sealed class TphContext(DbContextOptions options) : InheritanceContext(options)
    {
        protected override void ConfigureStrategy(ModelBuilder modelBuilder) =>
            modelBuilder.Entity<InheritanceRoot>().UseTphMappingStrategy();
    }

    private sealed class TptContext(DbContextOptions options) : InheritanceContext(options)
    {
        protected override void ConfigureStrategy(ModelBuilder modelBuilder) =>
            modelBuilder.Entity<InheritanceRoot>().UseTptMappingStrategy();
    }

    private sealed class TpcContext(DbContextOptions options) : InheritanceContext(options)
    {
        protected override void ConfigureStrategy(ModelBuilder modelBuilder) =>
            modelBuilder.Entity<InheritanceRoot>().UseTpcMappingStrategy();
    }

    private sealed class MissingMetadataContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InheritanceRoot>();
            modelBuilder.Entity<MissingMetadataEntity>();
            this.ModelCreatingDefaults(modelBuilder);
        }
    }

    private sealed class SourceKnownRootContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SourceKnownEntity>();
            modelBuilder.Entity<StandaloneEntity>();
            this.ModelCreatingDefaults(modelBuilder);
        }
    }
}
