using System.Diagnostics.CodeAnalysis;
using DRN.Framework.EntityFramework.Context;
using DRN.Framework.EntityFramework.Extensions;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Logging;
using Microsoft.EntityFrameworkCore;

namespace DRN.Test.Unit.Tests.Framework.EntityFramework;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public class DrnContextServiceRegistrationAttributeTests
{
    [Fact]
    public void GetEntityTypeValidationResult_Should_Detect_Multiple_AppIds()
    {
        Type[] domainTypes = [typeof(FirstPartitionEntity), typeof(SecondPartitionEntity)];

        var result = DrnContextServiceRegistrationAttribute.GetEntityTypeValidationResult(domainTypes);

        result.MissingEntityTypes.Should().BeEmpty();
        result.DuplicateEntityTypes.Should().BeEmpty();
        result.MultipleAppIds.Should().BeEquivalentTo([121, 122]);
    }

    [Fact]
    public void GetEntityTypeValidationResult_Should_Detect_Duplicate_EntityType_Within_Same_AppId()
    {
        Type[] domainTypes = [typeof(FirstPartitionEntity), typeof(FirstPartitionDuplicateEntity)];

        var result = DrnContextServiceRegistrationAttribute.GetEntityTypeValidationResult(domainTypes);

        result.MissingEntityTypes.Should().BeEmpty();
        result.DuplicateEntityTypes.Should().HaveCount(2);
        result.MultipleAppIds.Should().BeEmpty();
    }

    [Fact]
    public void GetEntityTypeValidationResult_Should_Allow_TestApp_With_Production_App()
    {
        Type[] domainTypes = [typeof(FirstPartitionEntity), typeof(TestPartitionEntity)];

        var result = DrnContextServiceRegistrationAttribute.GetEntityTypeValidationResult(domainTypes);

        result.MissingEntityTypes.Should().BeEmpty();
        result.DuplicateEntityTypes.Should().BeEmpty();
        result.MultipleAppIds.Should().BeEmpty();
        result.NonTestAppIds.Should().BeEquivalentTo([121]);
    }

    [Fact]
    public void ValidateEntityTypes_With_Mismatched_Configured_AppId_Should_Throw_ConfigurationException()
    {
        var options = new DbContextOptionsBuilder<TestValidationDbContext>()
            .UseNpgsql("Host=localhost;Database=test")
            .Options;
        using var dbContext = new TestValidationDbContext(options);

        var appSettings = Substitute.For<IAppSettings>();
        appSettings.NexusAppSettings.Returns(new NexusAppSettings { AppId = 55 });

        var act = () => DrnContextServiceRegistrationAttribute.ValidateEntityTypes(dbContext, scopedLog: null, appSettings);

        act.Should().ThrowExactly<ConfigurationException>()
            .WithMessage("*NexusAppSettings:AppId (55) does not match TestValidationDbContext domain partition AppId (121)*");
    }

    [Fact]
    public void ValidateEntityTypes_With_Zero_Configured_AppId_And_NonZero_Domain_Should_Throw_ConfigurationException()
    {
        var options = new DbContextOptionsBuilder<TestValidationDbContext>()
            .UseNpgsql("Host=localhost;Database=test")
            .Options;
        using var dbContext = new TestValidationDbContext(options);

        var zeroAppSettings = Substitute.For<IAppSettings>();
        zeroAppSettings.NexusAppSettings.Returns(new NexusAppSettings { AppId = 0 });

        var act = () => DrnContextServiceRegistrationAttribute.ValidateEntityTypes(dbContext, scopedLog: null, zeroAppSettings);

        act.Should().ThrowExactly<ConfigurationException>()
            .WithMessage("*NexusAppSettings:AppId (0) does not match TestValidationDbContext domain partition AppId (121)*");
    }

    [Fact]
    public void ValidateEntityTypes_With_Matching_Configured_AppId_Should_Succeed()
    {
        var options = new DbContextOptionsBuilder<TestValidationDbContext>()
            .UseNpgsql("Host=localhost;Database=test")
            .Options;
        using var dbContext = new TestValidationDbContext(options);

        var matchingAppSettings = Substitute.For<IAppSettings>();
        matchingAppSettings.NexusAppSettings.Returns(new NexusAppSettings { AppId = 121 });

        var actMatching = () => DrnContextServiceRegistrationAttribute.ValidateEntityTypes(dbContext, scopedLog: null, matchingAppSettings);

        actMatching.Should().NotThrow();
    }

    [Fact]
    public void ValidateEntityTypes_With_Zero_Configured_AppId_And_DefaultApp_Domain_Should_Succeed()
    {
        var options = new DbContextOptionsBuilder<TestDefaultValidationDbContext>()
            .UseNpgsql("Host=localhost;Database=test")
            .Options;
        using var dbContext = new TestDefaultValidationDbContext(options);

        var zeroAppSettings = Substitute.For<IAppSettings>();
        zeroAppSettings.NexusAppSettings.Returns(new NexusAppSettings { AppId = 0 });

        var act = () => DrnContextServiceRegistrationAttribute.ValidateEntityTypes(dbContext, scopedLog: null, zeroAppSettings);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateEntityTypes_With_MultiApp_Host_Should_Allow_Secondary_Partition_When_Primary_AppId_Is_Registered()
    {
        var options = new DbContextOptionsBuilder<TestSecondaryValidationDbContext>()
            .UseNpgsql("Host=localhost;Database=test")
            .Options;
        using var dbContext = new TestSecondaryValidationDbContext(options);

        // AppSettings is configured with primary partition (121)
        var primaryAppSettings = Substitute.For<IAppSettings>();
        primaryAppSettings.NexusAppSettings.Returns(new NexusAppSettings { AppId = 121 });

        var primaryOptions = new DbContextOptionsBuilder<TestValidationDbContext>()
            .UseNpgsql("Host=localhost;Database=test")
            .Options;
        using var primaryDbContext = new TestValidationDbContext(primaryOptions);

        var serviceProvider = Substitute.For<IServiceProvider>();
        var descriptor = new ServiceDescriptor(typeof(TestValidationDbContext), primaryDbContext);
        var module = new AttributeSpecifiedServiceModule([descriptor], new DrnContextServiceRegistrationAttribute());
        var container = new DrnServiceContainer(typeof(TestValidationDbContext).Assembly, [], [module]);
        serviceProvider.GetService(typeof(IEnumerable<DrnServiceContainer>)).Returns(new[] { container });
        serviceProvider.GetService(typeof(TestValidationDbContext)).Returns(primaryDbContext);

        // Secondary partition (122) in DbContext should succeed because primary partition (121) is registered in host containers
        var act = () => DrnContextServiceRegistrationAttribute.ValidateEntityTypes(dbContext, scopedLog: null, primaryAppSettings, serviceProvider);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateEntityTypes_With_MultiApp_Host_Should_Allow_Secondary_Partition_When_Primary_AppId_Is_Zero()
    {
        var options = new DbContextOptionsBuilder<TestSecondaryValidationDbContext>()
            .UseNpgsql("Host=localhost;Database=test")
            .Options;
        using var dbContext = new TestSecondaryValidationDbContext(options);

        // AppSettings is configured with primary partition (0 - DefaultApp)
        var zeroAppSettings = Substitute.For<IAppSettings>();
        zeroAppSettings.NexusAppSettings.Returns(new NexusAppSettings { AppId = 0 });

        var defaultOptions = new DbContextOptionsBuilder<TestDefaultValidationDbContext>()
            .UseNpgsql("Host=localhost;Database=test")
            .Options;
        using var defaultDbContext = new TestDefaultValidationDbContext(defaultOptions);

        var serviceProvider = Substitute.For<IServiceProvider>();
        var descriptor = new ServiceDescriptor(typeof(TestDefaultValidationDbContext), defaultDbContext);
        var module = new AttributeSpecifiedServiceModule([descriptor], new DrnContextServiceRegistrationAttribute());
        var container = new DrnServiceContainer(typeof(TestDefaultValidationDbContext).Assembly, [], [module]);
        serviceProvider.GetService(typeof(IEnumerable<DrnServiceContainer>)).Returns(new[] { container });
        serviceProvider.GetService(typeof(TestDefaultValidationDbContext)).Returns(defaultDbContext);

        // Secondary partition (122) in DbContext should succeed because primary partition (0) is registered in host containers
        var act = () => DrnContextServiceRegistrationAttribute.ValidateEntityTypes(dbContext, scopedLog: null, zeroAppSettings, serviceProvider);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateEntityTypes_With_Multiple_Production_AppIds_In_Same_DbContext_Should_Throw_UnprocessableEntityException()
    {
        var options = new DbContextOptionsBuilder<TestMultiAppValidationDbContext>()
            .UseNpgsql("Host=localhost;Database=test")
            .Options;
        using var dbContext = new TestMultiAppValidationDbContext(options);

        var matchingAppSettings = Substitute.For<IAppSettings>();
        matchingAppSettings.NexusAppSettings.Returns(new NexusAppSettings { AppId = 121 });

        var actNullLog = () => DrnContextServiceRegistrationAttribute.ValidateEntityTypes(dbContext, scopedLog: null, matchingAppSettings);
        actNullLog.Should().ThrowExactly<UnprocessableEntityException>()
            .WithMessage("*multipleAppIds*");

        var scopedLog = Substitute.For<IScopedLog>();
        var actWithLog = () => DrnContextServiceRegistrationAttribute.ValidateEntityTypes(dbContext, scopedLog, matchingAppSettings);
        actWithLog.Should().ThrowExactly<UnprocessableEntityException>()
            .WithMessage("*MultipleAppIds (121, 122)*");
    }

    [Fact]
    public void GetAllDomainEntityTypes_Should_Include_Both_Model_And_Assembly_Entities()
    {
        var options = new DbContextOptionsBuilder<TestValidationDbContext>()
            .UseNpgsql("Host=localhost;Database=test")
            .Options;
        using var dbContext = new TestValidationDbContext(options);

        var domainTypes = DrnContextServiceRegistrationAttribute.GetAllDomainEntityTypes(dbContext);

        domainTypes.Should().Contain(typeof(FirstPartitionEntity));
    }

    [Fact]
    public void GetModelDomainEntityTypes_Should_Include_Only_Model_Entities()
    {
        var options = new DbContextOptionsBuilder<TestValidationDbContext>()
            .UseNpgsql("Host=localhost;Database=test")
            .Options;
        using var dbContext = new TestValidationDbContext(options);

        var modelTypes = DrnContextServiceRegistrationAttribute.GetModelDomainEntityTypes(dbContext);
        modelTypes.Should().Contain(typeof(FirstPartitionEntity));
        modelTypes.Should().NotContain(typeof(SecondPartitionEntity));
    }

    [Fact]
    public void ValidateEntityTypes_With_CoLocated_SinglePartition_Contexts_In_NonTest_Assembly_Should_Succeed()
    {
        var firstOptions = new DbContextOptionsBuilder<DRN.MultiApp.Testing.CoLocatedFirstContext>()
            .UseNpgsql("Host=localhost;Database=test")
            .Options;
        using var firstDbContext = new DRN.MultiApp.Testing.CoLocatedFirstContext(firstOptions);

        var secondOptions = new DbContextOptionsBuilder<DRN.MultiApp.Testing.CoLocatedSecondContext>()
            .UseNpgsql("Host=localhost;Database=test")
            .Options;
        using var secondDbContext = new DRN.MultiApp.Testing.CoLocatedSecondContext(secondOptions);

        // Both contexts are co-located in DRN.MultiApp.Testing (non-test assembly)
        typeof(DRN.MultiApp.Testing.CoLocatedFirstContext).Assembly.FullName
            .Should().Be(typeof(DRN.MultiApp.Testing.CoLocatedSecondContext).Assembly.FullName);

        var firstAppSettings = Substitute.For<IAppSettings>();
        firstAppSettings.NexusAppSettings.Returns(new NexusAppSettings { AppId = DRN.MultiApp.Testing.CoLocatedFirstApp.AppId });

        var secondAppSettings = Substitute.For<IAppSettings>();
        secondAppSettings.NexusAppSettings.Returns(new NexusAppSettings { AppId = DRN.MultiApp.Testing.CoLocatedSecondApp.AppId });

        var actFirst = () => DrnContextServiceRegistrationAttribute.ValidateEntityTypes(firstDbContext, scopedLog: null, firstAppSettings);
        var actSecond = () => DrnContextServiceRegistrationAttribute.ValidateEntityTypes(secondDbContext, scopedLog: null, secondAppSettings);

        actFirst.Should().NotThrow();
        actSecond.Should().NotThrow();
    }

    [Fact]
    public void GetHostDomainEntityTypes_Should_Discover_Entities_From_Explicitly_Registered_Host_Assembly()
    {
        var serviceProvider = Substitute.For<IServiceProvider>();
        var testingContainer = new DrnServiceContainer(typeof(DRN.MultiApp.Testing.CoLocatedNonModelDomainEntity).Assembly, []);
        serviceProvider.GetService(typeof(IEnumerable<DrnServiceContainer>)).Returns(new[] { testingContainer });

        var hostEntities = DrnContextServiceRegistrationAttribute.GetHostDomainEntityTypes(serviceProvider, []);

        hostEntities.Should().Contain(typeof(DRN.MultiApp.Testing.CoLocatedFirstEntity));
        hostEntities.Should().Contain(typeof(DRN.MultiApp.Testing.CoLocatedSecondEntity));
        hostEntities.Should().Contain(typeof(DRN.MultiApp.Testing.CoLocatedNonModelDomainEntity));

        EntityTypeRegistry.Register(hostEntities);
        var entityTypeId = new EntityTypeId(2, DRN.MultiApp.Testing.CoLocatedFirstApp.AppId);
        EntityTypeRegistry.GetEntityType(entityTypeId).Should().Be(typeof(DRN.MultiApp.Testing.CoLocatedNonModelDomainEntity));
    }
}

public class TestValidationDbContext(DbContextOptions<TestValidationDbContext> options) : DbContext(options)
{
    public DbSet<FirstPartitionEntity> Entities => Set<FirstPartitionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        this.ModelCreatingDefaults(modelBuilder);
    }
}

public class TestMultiAppValidationDbContext(DbContextOptions<TestMultiAppValidationDbContext> options) : DbContext(options)
{
    public DbSet<FirstPartitionEntity> FirstEntities => Set<FirstPartitionEntity>();
    public DbSet<SecondPartitionEntity> SecondEntities => Set<SecondPartitionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        this.ModelCreatingDefaults(modelBuilder);
    }
}

public class TestSecondaryValidationDbContext(DbContextOptions<TestSecondaryValidationDbContext> options) : DbContext(options)
{
    public DbSet<SecondPartitionEntity> Entities => Set<SecondPartitionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        this.ModelCreatingDefaults(modelBuilder);
    }
}

public class TestDefaultValidationDbContext(DbContextOptions<TestDefaultValidationDbContext> options) : DbContext(options)
{
    public DbSet<DefaultPartitionEntity> Entities => Set<DefaultPartitionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        this.ModelCreatingDefaults(modelBuilder);
    }
}

public readonly struct FirstValidationApp : IAppId
{
    public const byte Value = 121;
    public static byte AppId => Value;
}

public readonly struct SecondValidationApp : IAppId
{
    public const byte Value = 122;
    public static byte AppId => Value;
}

[EntityType<FirstValidationApp>(250)]
public sealed class FirstPartitionEntity : SourceKnownEntity;

#pragma warning disable DRN0002 // Intentional duplicate entity type to test runtime validation
[EntityType<FirstValidationApp>(250)]
public sealed class FirstPartitionDuplicateEntity : SourceKnownEntity;
#pragma warning restore DRN0002

[EntityType<SecondValidationApp>(250)]
public sealed class SecondPartitionEntity : SourceKnownEntity;

[EntityType<TestApp>(250)]
public sealed class TestPartitionEntity : SourceKnownEntity;

[EntityType<DefaultApp>(250)]
public sealed class DefaultPartitionEntity : SourceKnownEntity;
