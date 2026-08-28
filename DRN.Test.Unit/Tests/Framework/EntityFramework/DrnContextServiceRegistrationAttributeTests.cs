using DRN.Framework.EntityFramework.Context;
using DRN.Framework.EntityFramework.Extensions;
using DRN.Framework.SharedKernel;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Settings;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace DRN.Test.Unit.Tests.Framework.EntityFramework;

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

        var serviceProvider = Substitute.For<IServiceProvider>();
        var container = new DrnServiceContainer(typeof(FirstPartitionEntity).Assembly, []);
        serviceProvider.GetService(typeof(IEnumerable<DrnServiceContainer>)).Returns(new[] { container });

        // Secondary partition (122) in DbContext should succeed because primary partition (121) is registered in host containers
        var act = () => DrnContextServiceRegistrationAttribute.ValidateEntityTypes(dbContext, scopedLog: null, primaryAppSettings, serviceProvider);

        act.Should().NotThrow();
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
    public static byte AppId => 121;
}

public readonly struct SecondValidationApp : IAppId
{
    public static byte AppId => 122;
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
