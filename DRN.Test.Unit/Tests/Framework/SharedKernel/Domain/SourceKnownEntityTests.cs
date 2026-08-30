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
        var customType = typeof(CustomTestEntity);
        SourceKnownEntity.GetAppId(customType).Should().Be(42);

        SourceKnownEntity.GetAppId<DefaultAppTestEntity>().Should().Be(0);
        var defaultType = typeof(DefaultAppTestEntity);
        SourceKnownEntity.GetAppId(defaultType).Should().Be(0);
    }

    [Fact]
    public void GetEntityTypeId_Generic_And_Type_Overloads_Should_Return_Composite_Id()
    {
        var customId = SourceKnownEntity.GetEntityTypeId<CustomTestEntity>();
        customId.EntityType.Should().Be(7);
        customId.AppId.Should().Be(42);

        var defaultType = typeof(DefaultAppTestEntity);
        var defaultId = SourceKnownEntity.GetEntityTypeId(defaultType);
        defaultId.EntityType.Should().Be(101);
        defaultId.AppId.Should().Be(0);
    }

    [Fact]
    public void GetEntityType_Should_Return_EntityType_Byte()
    {
        SourceKnownEntity.GetEntityType<CustomTestEntity>().Should().Be(7);
        var defaultType = typeof(DefaultAppTestEntity);
        SourceKnownEntity.GetEntityType(defaultType).Should().Be(101);
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

    [Fact]
    public void EntityTypeRegistry_Register_Should_Bulk_Register_And_Return_Correct_Values()
    {
        EntityTypeRegistry.Register([typeof(CustomTestEntity), typeof(DefaultAppTestEntity)]);

        var customId = EntityTypeRegistry.GetEntityTypeId(typeof(CustomTestEntity));
        customId.EntityType.Should().Be(7);
        customId.AppId.Should().Be(42);

        var retrievedType = EntityTypeRegistry.GetEntityType(customId);
        retrievedType.Should().Be<CustomTestEntity>();
    }

    [Fact]
    public void EntityTypeRegistry_GetEntityTypeId_With_Null_Should_Throw()
    {
        var act = () => EntityTypeRegistry.GetEntityTypeId(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EntityTypeRegistry_Register_With_Null_Should_Throw()
    {
        var act = () => EntityTypeRegistry.Register(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EntityTypeRegistry_Register_When_Called_Repeatedly_Should_Be_Idempotent()
    {
        EntityTypeRegistry.Register([typeof(CustomTestEntity)]);
        var actSecond = () => EntityTypeRegistry.Register([typeof(CustomTestEntity)]);
        actSecond.Should().NotThrow();

        var entityTypeId = EntityTypeRegistry.GetEntityTypeId(typeof(CustomTestEntity));
        entityTypeId.EntityType.Should().Be(7);
        entityTypeId.AppId.Should().Be(42);
    }

    [Fact]
    public void EntityTypeRegistry_Concurrent_Registrations_And_Reads_Should_Be_ThreadSafe()
    {
        const int threadCount = 10;
        const int iterations = 200;

        Parallel.For(0, threadCount, _ =>
        {
            for (var i = 0; i < iterations; i++)
            {
                EntityTypeRegistry.Register([typeof(CustomTestEntity), typeof(DefaultAppTestEntity)]);
                var customId = EntityTypeRegistry.GetEntityTypeId(typeof(CustomTestEntity));
                customId.EntityType.Should().Be(7);
                customId.AppId.Should().Be(42);

                var defaultId = EntityTypeRegistry.GetEntityTypeId(typeof(DefaultAppTestEntity));
                defaultId.EntityType.Should().Be(101);
                defaultId.AppId.Should().Be(0);

                EntityTypeRegistry.GetEntityType(customId).Should().Be<CustomTestEntity>();
                EntityTypeRegistry.GetEntityType(defaultId).Should().Be<DefaultAppTestEntity>();
            }
        });
    }
}
