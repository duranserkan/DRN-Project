using AwesomeAssertions;
using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.SharedKernel.Domain;

namespace DRN.Test.Unit.Tests.Framework.SharedKernel.Domain;

public readonly struct CustomTestApp : IAppId
{
    public const byte Value = 42;
    public static byte AppId => Value;
}

[EntityType<CustomTestApp>(7)]
public class CustomTestEntity : SourceKnownEntity;

[EntityType<DefaultApp>(101)]
public class DefaultAppTestEntity : SourceKnownEntity;

public class SourceKnownEntityTests
{
    [Fact]
    public void GetAppId_Generic_And_Type_Overloads_Should_Return_Declared_AppId()
    {
        SourceKnownEntity.GetAppId<CustomTestEntity>().Should().Be(42);
        SourceKnownEntity.GetAppId(typeof(CustomTestEntity)).Should().Be(42);

        SourceKnownEntity.GetAppId<DefaultAppTestEntity>().Should().Be(0);
        SourceKnownEntity.GetAppId(typeof(DefaultAppTestEntity)).Should().Be(0);
    }

    [Fact]
    public void GetEntityTypeId_Generic_And_Type_Overloads_Should_Return_Composite_Id()
    {
        var customId = SourceKnownEntity.GetEntityTypeId<CustomTestEntity>();
        customId.EntityType.Should().Be(7);
        customId.AppId.Should().Be(42);

        var defaultId = SourceKnownEntity.GetEntityTypeId(typeof(DefaultAppTestEntity));
        defaultId.EntityType.Should().Be(101);
        defaultId.AppId.Should().Be(0);
    }

    [Fact]
    public void GetEntityType_Should_Return_EntityType_Byte()
    {
        SourceKnownEntity.GetEntityType<CustomTestEntity>().Should().Be(7);
        SourceKnownEntity.GetEntityType(typeof(DefaultAppTestEntity)).Should().Be(101);
    }

    [Fact]
    public void Validate_When_EntityType_Matches_But_AppId_Differs_Should_Throw_ValidationException()
    {
        // Entity in AppId 42 with EntityType 7
        var entityType = (byte)7;
        var customSkid = new SourceKnownId(12345, DateTimeOffset.UtcNow, 1, 42, 1);
        var customId = new SourceKnownEntityId(customSkid, Guid.NewGuid(), entityType, true, false);

        // Validating against AppId 42, EntityType 7 succeeds
        var actValid = () => customId.Validate<CustomTestEntity>();
        actValid.Should().NotThrow();

        // Validating against AppId 0, EntityType 7 fails even if entity type byte were to match
        var actInvalidApp = () => customId.Validate(new EntityTypeId(7, 0));
        actInvalidApp.Should().Throw<ValidationException>();

        // Validating against DefaultAppTestEntity (AppId 0, EntityType 101) fails
        var actDefault = () => customId.Validate<DefaultAppTestEntity>();
        actDefault.Should().Throw<ValidationException>();
    }
}
