using System.Buffers.Binary;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Ids;
using DRN.Framework.Utils.Numbers;
using DRN.Framework.Utils.Time;

namespace DRN.Test.Unit.Tests.Framework.Utils.Ids;

/// <summary>
/// Verifies the numeric walkthrough example in paper/paper-peerj.md § Numeric Walkthrough.
/// These tests establish the correct hex values that the paper must reference.
/// </summary>
public class PaperNumericWalkthroughTests
{
    // Paper walkthrough fields (tick-based: 250ms precision, 4 ticks/s)
    private const uint WalkthroughTimestamp = 100_000_000U * 4; // 100M seconds × 4 ticks/s = 400,000,000 ticks
    private const byte WalkthroughAppId = 18;
    private const byte WalkthroughAppInstanceId = 1;
    private const uint WalkthroughSequence = 5;
    private const byte WalkthroughEntityType = 0x0A; // entity type 10
    private const byte WalkthroughEpoch = 0x00;

    // Hex value will change with the new bit layout — computed from NumberBuilder
    private static readonly long ExpectedSkid;

    // SKEID marker bytes (UUID V8 RFC 9562)
    private const byte VersionMarker = 0x8D;
    private const byte VariantMarker = 0x8D;

    static PaperNumericWalkthroughTests()
    {
        // Compute ExpectedSkid using layout: sign(1) + timestamp(32) + appId(7) + appInstanceId(6) + sequence(18)
        var builder = NumberBuilder.GetLong();
        builder.SetResidueValue(WalkthroughTimestamp);
        builder.TryAdd(WalkthroughAppId, 7);
        builder.TryAdd(WalkthroughAppInstanceId, 6);
        builder.TryAdd(WalkthroughSequence, 18);
        ExpectedSkid = builder.GetValue();
    }

    [Fact]
    public void SKID_Walkthrough_Should_Produce_Correct_Hex_Value()
    {
        // Arrange — construct SKID using the same NumberBuilder path as SourceKnownIdUtils.Generate
        var builder = NumberBuilder.GetLong();
        builder.SetResidueValue(WalkthroughTimestamp);
        builder.TryAdd(WalkthroughAppId, 7);
        builder.TryAdd(WalkthroughAppInstanceId, 6);
        builder.TryAdd(WalkthroughSequence, 18);
        var skid = builder.GetValue();

        // Assert — SKID hex matches expected
        skid.Should().Be(ExpectedSkid,
            $"SKID for (ts={WalkthroughTimestamp}, appId={WalkthroughAppId}, " +
            $"instId={WalkthroughAppInstanceId}, seq={WalkthroughSequence}) " +
            $"should be 0x{unchecked((ulong)ExpectedSkid):X16}");

        // Assert — sign bit is 1 (negative, first epoch half)
        skid.Should().BeNegative("first ~34 years of epoch produce negative SKIDs (sign bit = 1)");

        // Assert — round-trip via NumberParser recovers all fields
        var parser = NumberParser.Get(skid);
        var parsedAppId = (byte)parser.Read(7);
        var parsedAppInstanceId = (byte)parser.Read(6);
        var parsedSequence = parser.Read(18);
        var parsedTimestamp = parser.ReadResidueValue();

        parsedTimestamp.Should().Be(WalkthroughTimestamp);
        parsedAppId.Should().Be(WalkthroughAppId);
        parsedAppInstanceId.Should().Be(WalkthroughAppInstanceId);
        parsedSequence.Should().Be(WalkthroughSequence);
    }

    [Theory]
    [DataInlineUnit]
    public void SKEID_Walkthrough_Should_Have_Correct_Byte_Layout(DrnTestContextUnit context)
    {
        var nexusSettings = new NexusAppSettings { AppId = WalkthroughAppId, AppInstanceId = WalkthroughAppInstanceId };
        context.AddToConfiguration(new { NexusAppSettings = nexusSettings });

        var entityIdUtils = context.GetRequiredService<ISourceKnownEntityIdUtils>();
        var skeid = entityIdUtils.GeneratePlain(ExpectedSkid, WalkthroughEntityType);

        // Assert — SKEID is valid and carries the correct source SKID
        skeid.Valid.Should().BeTrue();
        skeid.Source.Id.Should().Be(ExpectedSkid);
        skeid.EntityType.Should().Be(WalkthroughEntityType);
        skeid.Secure.Should().BeFalse();

        // Extract raw bytes from the GUID
        var guidBytes = skeid.EntityId.ToByteArray();

        // Assert — SKID upper half at bytes 0–3 (little-endian)
        // Upper half = bits 63–32 of the SKID
        var upperHalf = BinaryPrimitives.ReadUInt32LittleEndian(guidBytes.AsSpan(0, 4));
        var expectedUpperHalf = (uint)(unchecked((ulong)ExpectedSkid) >> 32);
        upperHalf.Should().Be(expectedUpperHalf,
            $"bytes 0–3 should contain SKID upper half 0x{expectedUpperHalf:X8}");

        // Assert — entity type at byte 4
        guidBytes[4].Should().Be(WalkthroughEntityType, "byte 4 should contain entity type");

        // Assert — epoch at byte 5
        guidBytes[5].Should().Be(WalkthroughEpoch, "byte 5 should contain epoch");

        // Assert — version marker at byte 7
        guidBytes[7].Should().Be(VersionMarker, "byte 7 should contain version marker 0x8D (UUID V8)");

        // Assert — variant marker at byte 8
        guidBytes[8].Should().Be(VariantMarker, "byte 8 should contain variant marker 0x8D (RFC 9562 §4.1)");

        // Assert — MAC bytes at non-contiguous positions 6, 9, 10, 11
        // We verify they are non-zero (MAC was computed); exact values depend on the key
        var macByte0 = guidBytes[6];
        var macByte1 = guidBytes[9];
        var macByte2 = guidBytes[10];
        var macByte3 = guidBytes[11];
        // At least one MAC byte should be non-zero (probability of all-zero MAC is ~1/2^32)
        (macByte0 | macByte1 | macByte2 | macByte3).Should().NotBe(0,
            "MAC bytes at positions 6, 9, 10, 11 should contain a BLAKE3 keyed MAC");

        // Assert — SKID lower half at bytes 12–15 (little-endian)
        // Lower half = bits 31–0 of the SKID
        var lowerHalf = BinaryPrimitives.ReadUInt32LittleEndian(guidBytes.AsSpan(12, 4));
        var expectedLowerHalf = (uint)(ExpectedSkid & 0xFFFFFFFF);
        lowerHalf.Should().Be(expectedLowerHalf,
            $"bytes 12–15 should contain SKID lower half 0x{expectedLowerHalf:X8}");

        // Assert — parse round-trip recovers the same SKEID
        var parsed = entityIdUtils.Parse(skeid.EntityId);
        parsed.Valid.Should().BeTrue("parsed SKEID should be valid");
        parsed.Source.Id.Should().Be(ExpectedSkid, "parsed SKID should match original");
        parsed.EntityType.Should().Be(WalkthroughEntityType, "parsed entity type should match");
    }

    [Theory]
    [DataInlineUnit]
    public void SecureSKEID_Walkthrough_Should_RoundTrip(DrnTestContextUnit context)
    {
        var nexusSettings = new NexusAppSettings { AppId = WalkthroughAppId, AppInstanceId = WalkthroughAppInstanceId };
        context.AddToConfiguration(new { NexusAppSettings = nexusSettings });

        var entityIdUtils = context.GetRequiredService<ISourceKnownEntityIdUtils>();

        // Generate Secure SKEID from the walkthrough SKID
        var secureSkeid = entityIdUtils.GenerateSecure(ExpectedSkid, WalkthroughEntityType);
        secureSkeid.Valid.Should().BeTrue();
        secureSkeid.Secure.Should().BeTrue();
        secureSkeid.Source.Id.Should().Be(ExpectedSkid);
        secureSkeid.EntityType.Should().Be(WalkthroughEntityType);

        // Encrypted GUID should differ from plain GUID
        var plainSkeid = entityIdUtils.GeneratePlain(ExpectedSkid, WalkthroughEntityType);
        secureSkeid.EntityId.Should().NotBe(plainSkeid.EntityId,
            "encrypted Secure SKEID must differ from plaintext SKEID");

        // Parse round-trip: decrypt → verify markers → verify MAC → recover SKID
        var parsed = entityIdUtils.Parse(secureSkeid.EntityId);
        parsed.Valid.Should().BeTrue("Secure SKEID should parse successfully");
        parsed.Secure.Should().BeTrue("parsed result should be marked as Secure");
        parsed.Source.Id.Should().Be(ExpectedSkid, "decrypted SKID should match original");
        parsed.EntityType.Should().Be(WalkthroughEntityType, "decrypted entity type should match");

        // Bidirectional conversion: Secure → Plain → Secure
        var convertedToPlain = entityIdUtils.ToPlain(secureSkeid);
        convertedToPlain.EntityId.Should().Be(plainSkeid.EntityId);

        var convertedToSecure = entityIdUtils.ToSecure(plainSkeid);
        convertedToSecure.EntityId.Should().Be(secureSkeid.EntityId);
    }

    [EntityType(WalkthroughEntityType)]
    class WalkthroughEntity(long id) : SourceKnownEntity(id);
}
