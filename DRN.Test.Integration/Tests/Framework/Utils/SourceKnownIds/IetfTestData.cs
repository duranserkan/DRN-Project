using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Data.Encodings;
using DRN.Framework.Utils.Ids;

namespace DRN.Test.Integration.Tests.Framework.Utils.SourceKnownIds;

public record FormattedGuidPayload(Guid PlainId, Guid SecureId, string? Description = null);

public record NullableGuidPayload(Guid? NullablePlainId, Guid? NullableSecureId);

public readonly struct IetfDraftTestApp : IAppId
{
    public const byte Value = 5;
    public static byte AppId => Value;
}

public enum IetfDraftEntityTypes : byte
{
    TestEntity = 1,
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class IetfDraftEntityTypeAttribute(IetfDraftEntityTypes entityType)
    : EntityTypeAttribute<IetfDraftTestApp>((byte)entityType);

[IetfDraftEntityType(IetfDraftEntityTypes.TestEntity)]
public class IetfDraftTestEntity(long id = 0) : SourceKnownEntity(id)
{
    public Guid PlainId { get; init; }
    public Guid SecureId { get; init; }
    public string Name { get; init; } = string.Empty;
}

public class IetfDraftTestDto(SourceKnownEntity? entity = null) : Dto(entity)
{
    public Guid PlainId { get; init; }
    public Guid SecureId { get; init; }
    public string Name { get; init; } = string.Empty;
}

public static class IetfTestData
{
    // Values from IETF SKID draft Appendix A & DRN.Test.Unit/Tests/Framework/Utils/Ids/IetfDraftTestVectorTests.cs
    public const byte TestAppId = 5;
    public const byte TestAppInstanceId = 3;
    public const long TestSkidDecimal = -8639256484514103254L;
    public const string PlainGuidHex = "00081B3200058D018D0C002A492C0E75";
    public const string SecureGuidHex = "652068A43612CC4B8ABB83B853DC6786";
    public static readonly Guid PlainGuid = Guid.Parse(PlainGuidHex);
    public static readonly Guid SecureGuid = Guid.Parse(SecureGuidHex);
    public const byte SourceKnownMarkerVersion = 8; // V8 => UUID V8 per RFC 9562 §5.8
    public const byte SourceKnownMarkerVariant = 8; // Variant RFC 9562 §4.1
    public const byte SourceKnownSecureVersion = 12; // V8 => Not compatible with RFC 9562
    public const string TestNexusKeyMaterialHex = "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F";

    public static NexusAppSettings CreateNexusAppSettings() => new()
    {
        AppId = TestAppId,
        AppInstanceId = TestAppInstanceId,
        Keys = [new NexusKey(TestNexusKeyMaterialHex, ByteEncoding.Hex) { Default = true }]
    };

    public static (ISourceKnownEntityIdUtils Utils, SourceKnownEntityId Plain, SourceKnownEntityId Secure) Setup(DrnTestContext context)
    {
        var nexusAppSettings = CreateNexusAppSettings();
        var appSettings = SettingsProvider.DevelopmentWithNexusSettings<IetfDraftTestApp>(nexusAppSettings);
        context.ServiceCollection.ReplaceSingleton<IAppSettings, AppSettings>(appSettings);

        var utils = context.GetRequiredService<ISourceKnownEntityIdUtils>();
        var plain = utils.Parse(PlainGuid, SourceKnownEntityIdFormat.Plain);
        var secure = utils.Parse(SecureGuid, SourceKnownEntityIdFormat.Secure);

        PlainGuid.Version.Should().Be(SourceKnownMarkerVersion);
        PlainGuid.Variant.Should().Be(SourceKnownMarkerVariant);
        SecureGuid.Version.Should().Be(SourceKnownSecureVersion);
        SecureGuid.Variant.Should().Be(SourceKnownMarkerVariant);

        plain.Valid.Should().BeTrue();
        plain.Secure.Should().BeFalse();
        secure.Valid.Should().BeTrue();
        secure.Secure.Should().BeTrue();

        return (utils, plain, secure);
    }

    public static void ShouldBeEquivalentSkeid(
        this ISourceKnownEntityIdUtils utils,
        Guid actualGuid,
        SourceKnownEntityId expected,
        SourceKnownEntityIdFormat format)
    {
        actualGuid.Should().Be(expected.EntityId);
        var parsed = utils.Parse(actualGuid, format);
        parsed.Valid.Should().BeTrue();
        parsed.Secure.Should().Be(expected.Secure);
        parsed.EntityType.Should().Be(expected.EntityType);
        parsed.Source.Id.Should().Be(expected.Source.Id);
        parsed.Source.AppId.Should().Be(expected.Source.AppId);
        parsed.Source.AppInstanceId.Should().Be(expected.Source.AppInstanceId);
        parsed.Source.CreatedAt.Should().Be(expected.Source.CreatedAt);
    }
}
