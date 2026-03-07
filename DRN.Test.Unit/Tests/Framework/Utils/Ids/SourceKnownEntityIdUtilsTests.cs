using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Ids;
using DRN.Framework.Utils.Time;

namespace DRN.Test.Unit.Tests.Framework.Utils.Ids;

public class SourceKnownEntityIdUtilsTests
{
    [Theory]
    [DataInlineUnit]
    public async Task ExplicitMethods_Should_Generate_Valid_Secure_And_Unsecure_Ids(DrnTestContextUnit context)
    {
        var nexusSettings = new NexusAppSettings { AppId = 5, AppInstanceId = 12 };
        context.AddToConfiguration(new { NexusAppSettings = nexusSettings });

        var idUtils = context.GetRequiredService<ISourceKnownIdUtils>();
        var entityIdUtils = context.GetRequiredService<ISourceKnownEntityIdUtils>();
        var xEntityType = SourceKnownEntity.GetEntityType<XEntity>();

        var epoch = EpochTimeUtils.Epoch2025;
        var beforeIdGenerated = DateTimeOffset.UtcNow;

        await Task.Delay(1100); // buffer to compensate id-generator caching effect
        var longId1 = idUtils.Next<XEntity>();
        var entity1 = new XEntity(longId1);
        var entity1Dup = new XEntity(longId1);

        // Generate both variants from same source ID
        var unsecureId1 = entityIdUtils.GenerateUnsecure(entity1);
        var secureId1 = entityIdUtils.GenerateSecure(entity1Dup);
        entity1.EntityIdSource = unsecureId1;
        await Task.Delay(1000);
        var afterIdGenerated = DateTimeOffset.UtcNow;

        // Assert both are valid with correct properties
        AssertValidEntityId(unsecureId1, longId1, nexusSettings, xEntityType, expectedSecure: false);
        AssertValidEntityId(secureId1, longId1, nexusSettings, xEntityType, expectedSecure: true);
        
        // Parse roundtrip — both variants
        entityIdUtils.Parse(null).Should().BeNull();
        AssertParseRoundTrip(entityIdUtils, unsecureId1);
        AssertParseRoundTrip(entityIdUtils, secureId1);

        // Detailed parse equality assertions
        var parsedUnsecure = entityIdUtils.Parse(unsecureId1.EntityId);
        parsedUnsecure.Should().Be(unsecureId1);
        parsedUnsecure.EntityId.Should().Be(unsecureId1.EntityId);

        // Timestamp assertions via parsed source id
        var idInfo = idUtils.Parse(unsecureId1.Source.Id);
        idInfo.AppId.Should().Be(nexusSettings.AppId);
        idInfo.AppInstanceId.Should().Be(nexusSettings.AppInstanceId);
        idInfo.CreatedAt.Should().BeBefore(afterIdGenerated);
        idInfo.CreatedAt.Should().BeAfter(beforeIdGenerated);
        epoch.Should().BeBefore(beforeIdGenerated);

        // Secure parse — parsed result carries the encrypted EntityId Guid
        var parsedSecure = entityIdUtils.Parse(secureId1.EntityId);
        parsedSecure.Should().Be(secureId1);
        parsedSecure.EntityId.Should().Be(secureId1.EntityId);

        // Secure and unsecure parse to same source but different GUIDs
        parsedUnsecure.Source.Should().Be(parsedSecure.Source);
        parsedUnsecure.EntityType.Should().Be(parsedSecure.EntityType);
        unsecureId1.EntityId.Should().NotBe(secureId1.EntityId);

        // Ordering and comparison operators
        var longId2 = idUtils.Next<XEntity>();
        var unsecureId2 = entityIdUtils.GenerateUnsecure(new XEntity(longId2));
        var unsecureId1Again = entityIdUtils.GenerateUnsecure(new XEntity(longId1));
        (unsecureId2 > unsecureId1).Should().BeTrue();
        (unsecureId2 >= unsecureId1).Should().BeTrue();

        var unsecureId2Dup = entityIdUtils.GenerateUnsecure(new XEntity(longId2));
        (unsecureId2 >= unsecureId2Dup).Should().BeTrue();
        (unsecureId1 < unsecureId2).Should().BeTrue();
        (unsecureId1 <= unsecureId2).Should().BeTrue();
        (unsecureId1 <= unsecureId1Again).Should().BeTrue();

        // Same entity type check
        unsecureId1.HasSameEntityType(unsecureId2).Should().BeTrue();
        secureId1.HasSameEntityType(entityIdUtils.GenerateSecure(new XEntity(longId2))).Should().BeTrue();

        // Cross entity type
        var unsecureY = entityIdUtils.GenerateUnsecure(new YEntity(longId2));
        unsecureId2.HasSameEntityType(unsecureY).Should().BeFalse();

        // Generic overloads
        var secureGeneric = entityIdUtils.GenerateSecure<XEntity>(longId1);
        AssertValidEntityId(secureGeneric, longId1, nexusSettings, xEntityType, expectedSecure: true);

        var unsecureGeneric = entityIdUtils.GenerateUnsecure<XEntity>(longId1);
        AssertValidEntityId(unsecureGeneric, longId1, nexusSettings, xEntityType, expectedSecure: false);
    }

    [Theory]
    [DataInlineUnit(true)]
    [DataInlineUnit(false)]
    public async Task Generate_Should_Dispatch_Based_On_UseSecureFlag(DrnTestContextUnit context, bool secure)
    {
        var nexusSettings = new NexusAppSettings
        {
            AppId = 5,
            AppInstanceId = 12,
            UseSecureSourceKnownIds = secure
        };
        context.AddToConfiguration(new { NexusAppSettings = nexusSettings });
        var idUtils = context.GetRequiredService<ISourceKnownIdUtils>();
        var entityIdUtils = context.GetRequiredService<ISourceKnownEntityIdUtils>();

        await Task.Delay(1100); // buffer to compensate id-generator caching effect
        var longId = idUtils.Next<XEntity>();

        // Generate() should dispatch to secure/unsecure path based on flag
        var entityId = entityIdUtils.Generate(new XEntity(longId));
        AssertValidEntityId(entityId, longId, nexusSettings, SourceKnownEntity.GetEntityType<XEntity>(), expectedSecure: secure);
        AssertParseRoundTrip(entityIdUtils, entityId);

        // Explicit methods should always bypass the flag
        var explicitUnsecure = entityIdUtils.GenerateUnsecure(new XEntity(longId));
        explicitUnsecure.Secure.Should().BeFalse("GenerateUnsecure should always set Secure=false");
        AssertPlaintextMarkers(explicitUnsecure, true, "GenerateUnsecure should always produce plaintext markers");
        AssertParseRoundTrip(entityIdUtils, explicitUnsecure);

        var explicitSecure = entityIdUtils.GenerateSecure(new XEntity(longId));
        explicitSecure.Secure.Should().BeTrue("GenerateSecure should always set Secure=true");
        AssertParseRoundTrip(entityIdUtils, explicitSecure);
    }

    [Theory]
    [DataInlineUnit]
    public async Task SecureParse_Should_Fail_On_Tampered_Guid(DrnTestContextUnit context)
    {
        var nexusSettings = new NexusAppSettings { AppId = 5, AppInstanceId = 12 };
        context.AddToConfiguration(new { NexusAppSettings = nexusSettings });
        var idUtils = context.GetRequiredService<ISourceKnownIdUtils>();
        var entityIdUtils = context.GetRequiredService<ISourceKnownEntityIdUtils>();

        await Task.Delay(1100); // buffer to compensate id-generator caching effect
        var longId = idUtils.Next<XEntity>();
        var secureEntityId = entityIdUtils.GenerateSecure(new XEntity(longId));

        // Tamper with first byte — AES-ECB decryption produces garbage, markers won't match
        var tamperedBytes = secureEntityId.EntityId.ToByteArray();
        tamperedBytes[0] ^= 0xFF;
        var tamperedResult1 = entityIdUtils.Parse(new Guid(tamperedBytes));
        tamperedResult1.Valid.Should().BeFalse("tampered secure guid should be detected as invalid");
        tamperedResult1.Secure.Should().BeFalse("invalid result should have Secure=false");

        // Tamper with a different byte position
        var tamperedBytes2 = secureEntityId.EntityId.ToByteArray();
        tamperedBytes2[4] ^= 0x01;
        var tamperedResult2 = entityIdUtils.Parse(new Guid(tamperedBytes2));
        tamperedResult2.Valid.Should().BeFalse("any tampered byte should be detected as invalid");
        tamperedResult2.Secure.Should().BeFalse("invalid result should have Secure=false");
    }

    [Theory]
    [DataInlineUnit]
    public async Task SecureValidate_Should_Work_With_Multiple_EntityTypes(DrnTestContextUnit context)
    {
        var nexusSettings = new NexusAppSettings { AppId = 5, AppInstanceId = 12 };
        context.AddToConfiguration(new { NexusAppSettings = nexusSettings });
        var idUtils = context.GetRequiredService<ISourceKnownIdUtils>();
        var entityIdUtils = context.GetRequiredService<ISourceKnownEntityIdUtils>();

        await Task.Delay(1100); // buffer to compensate id-generator caching effect
        var longId = idUtils.Next<XEntity>();

        var secureX = entityIdUtils.GenerateSecure<XEntity>(longId);
        var secureY = entityIdUtils.GenerateSecure<YEntity>(longId);

        // Different entity types should produce different encrypted guids
        secureX.EntityId.Should().NotBe(secureY.EntityId);
        secureX.EntityType.Should().NotBe(secureY.EntityType);

        // Validate should work correctly for matching types
        var validatedX = entityIdUtils.Validate<XEntity>(secureX.EntityId);
        validatedX.Valid.Should().BeTrue();
        validatedX.Secure.Should().BeTrue("validated secure id should preserve Secure=true");
        validatedX.EntityType.Should().Be(SourceKnownEntity.GetEntityType<XEntity>());

        var validatedY = entityIdUtils.Validate<YEntity>(secureY.EntityId);
        validatedY.Valid.Should().BeTrue();
        validatedY.Secure.Should().BeTrue("validated secure id should preserve Secure=true");
        validatedY.EntityType.Should().Be(SourceKnownEntity.GetEntityType<YEntity>());

        // HasSameEntityType should work correctly after parsing
        var parsedX = entityIdUtils.Parse(secureX.EntityId);
        var parsedY = entityIdUtils.Parse(secureY.EntityId);
        parsedX.Secure.Should().BeTrue("parsed secure id should have Secure=true");
        parsedY.Secure.Should().BeTrue("parsed secure id should have Secure=true");
        parsedX.HasSameEntityType(parsedY).Should().BeFalse();
        parsedX.HasSameEntityType<XEntity>().Should().BeTrue();
        parsedY.HasSameEntityType<YEntity>().Should().BeTrue();

        // Cross-type validation should throw
        var act = () => entityIdUtils.Validate<YEntity>(secureX.EntityId);
        act.Should().Throw<ValidationException>();
    }

    [Theory]
    [DataInlineUnit]
    public async Task ToSecure_And_ToUnsecure_Should_Convert_And_Be_Idempotent(DrnTestContextUnit context)
    {
        var nexusSettings = new NexusAppSettings { AppId = 5, AppInstanceId = 12 };
        context.AddToConfiguration(new { NexusAppSettings = nexusSettings });
        var idUtils = context.GetRequiredService<ISourceKnownIdUtils>();
        var entityIdUtils = context.GetRequiredService<ISourceKnownEntityIdUtils>();

        await Task.Delay(1100); // buffer to compensate id-generator caching effect
        var longId = idUtils.Next<XEntity>();

        var unsecureId = entityIdUtils.GenerateUnsecure(new XEntity(longId));
        var secureId = entityIdUtils.GenerateSecure(new XEntity(longId));

        // Unsecure → Secure conversion
        var convertedToSecure = entityIdUtils.ToSecure(unsecureId);
        convertedToSecure.Secure.Should().BeTrue("converted id should be secure");
        convertedToSecure.Source.Should().Be(unsecureId.Source, "Source must be preserved");
        convertedToSecure.EntityType.Should().Be(unsecureId.EntityType, "EntityType must be preserved");
        convertedToSecure.Valid.Should().BeTrue("converted id must be valid");
        convertedToSecure.EntityId.Should().Be(secureId.EntityId, "converted GUID must match directly-generated secure GUID");
        AssertParseRoundTrip(entityIdUtils, convertedToSecure);

        // Secure → Unsecure conversion
        var convertedToUnsecure = entityIdUtils.ToUnsecure(secureId);
        convertedToUnsecure.Secure.Should().BeFalse("converted id should be unsecure");
        convertedToUnsecure.Source.Should().Be(secureId.Source, "Source must be preserved");
        convertedToUnsecure.EntityType.Should().Be(secureId.EntityType, "EntityType must be preserved");
        convertedToUnsecure.Valid.Should().BeTrue("converted id must be valid");
        convertedToUnsecure.EntityId.Should().Be(unsecureId.EntityId, "converted GUID must match directly-generated unsecure GUID");
        AssertParseRoundTrip(entityIdUtils, convertedToUnsecure);

        // Idempotency — ToSecure on already-secure returns same
        var secureAgain = entityIdUtils.ToSecure(secureId);
        secureAgain.Should().Be(secureId, "ToSecure on secure id should return same id");

        // Idempotency — ToUnsecure on already-unsecure returns same
        var unsecureAgain = entityIdUtils.ToUnsecure(unsecureId);
        unsecureAgain.Should().Be(unsecureId, "ToUnsecure on unsecure id should return same id");

        // Nullable overloads
        entityIdUtils.ToSecure((SourceKnownEntityId?)null).Should().BeNull();
        entityIdUtils.ToUnsecure((SourceKnownEntityId?)null).Should().BeNull();
        entityIdUtils.ToSecure((SourceKnownEntityId?)unsecureId).Should().Be(convertedToSecure);
        entityIdUtils.ToUnsecure((SourceKnownEntityId?)secureId).Should().Be(convertedToUnsecure);

        // Invalid id should throw
        var invalidId = new SourceKnownEntityId(default, Guid.NewGuid(), byte.MaxValue, false, Secure: false);
        var actSecure = () => entityIdUtils.ToSecure(invalidId);
        actSecure.Should().Throw<ValidationException>();
        var actUnsecure = () => entityIdUtils.ToUnsecure(invalidId);
        actUnsecure.Should().Throw<ValidationException>();
    }

    private static void AssertValidEntityId(SourceKnownEntityId entityId, long expectedId,
        NexusAppSettings nexusSettings, byte expectedEntityType, bool expectedSecure)
    {
        entityId.Valid.Should().BeTrue();
        entityId.Source.Id.Should().Be(expectedId);
        entityId.Source.AppId.Should().Be(nexusSettings.AppId);
        entityId.Source.AppInstanceId.Should().Be(nexusSettings.AppInstanceId);
        entityId.EntityType.Should().Be(expectedEntityType);
        entityId.Secure.Should().Be(expectedSecure);

        // Secure flag ↔ EntityId GUID content invariant:
        // Unsecure: plaintext GUID — deterministic markers (4D8D) and RFC 4122 V4 compliance
        // Secure:   AES-encrypted GUID — markers/V4 assertions skipped because ciphertext can
        //           coincidentally produce 4D8D marker bytes (~1/65536, see SourceKnownEntityIdUtils L169)
        if (expectedSecure) return;
        
        AssertPlaintextMarkers(entityId, true, "Unsecure EntityId must contain plaintext 4D8D markers");
        IsVersion4Rfc4122(entityId.EntityId).Should().BeTrue("Unsecure EntityId should be RFC 4122 V4 compliant");
    }

    private static void AssertParseRoundTrip(ISourceKnownEntityIdUtils entityIdUtils, SourceKnownEntityId original)
    {
        var parsed = entityIdUtils.Parse(original.EntityId);
        parsed.Valid.Should().BeTrue();
        parsed.Source.Id.Should().Be(original.Source.Id);
        parsed.Source.Should().Be(original.Source);
        parsed.EntityType.Should().Be(original.EntityType);
        parsed.Secure.Should().Be(original.Secure);
    }

    private static void AssertPlaintextMarkers(SourceKnownEntityId entityId, bool shouldHaveMarkers, string because)
    {
        var bytes = entityId.EntityId.ToByteArray();
        var hasMarkers = bytes[7] == 0x4D && bytes[8] == 0x8D;
        hasMarkers.Should().Be(shouldHaveMarkers, because);
    }

    private static bool IsVersion4Rfc4122(Guid guid)
    {
        var bytes = guid.ToByteArray();
        var isVersion4 = (bytes[7] >> 4) == 4;
        var isRfc4122Variant = (bytes[8] & 0xC0) == 0x80;
        return isVersion4 && isRfc4122Variant;
    }

    [EntityType(200)]
    class XEntity(long id) : SourceKnownEntity(id);
    [EntityType(201)]
    class YEntity(long id) : SourceKnownEntity(id);
}
