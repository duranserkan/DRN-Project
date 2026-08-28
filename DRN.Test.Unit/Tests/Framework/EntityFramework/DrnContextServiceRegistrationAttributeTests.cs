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
    public void ValidateEntityTypes_With_Matching_Or_Zero_Configured_AppId_Should_Succeed()
    {
        var options = new DbContextOptionsBuilder<TestValidationDbContext>()
            .UseNpgsql("Host=localhost;Database=test")
            .Options;
        using var dbContext = new TestValidationDbContext(options);

        var matchingAppSettings = Substitute.For<IAppSettings>();
        matchingAppSettings.NexusAppSettings.Returns(new NexusAppSettings { AppId = 121 });

        var zeroAppSettings = Substitute.For<IAppSettings>();
        zeroAppSettings.NexusAppSettings.Returns(new NexusAppSettings { AppId = 0 });

        var actMatching = () => DrnContextServiceRegistrationAttribute.ValidateEntityTypes(dbContext, scopedLog: null, matchingAppSettings);
        var actZero = () => DrnContextServiceRegistrationAttribute.ValidateEntityTypes(dbContext, scopedLog: null, zeroAppSettings);

        actMatching.Should().NotThrow();
        actZero.Should().NotThrow();
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
