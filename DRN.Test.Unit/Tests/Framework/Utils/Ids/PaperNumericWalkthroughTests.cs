using System.Buffers.Binary;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Ids;
using DRN.Framework.Utils.Numbers;
using DRN.Framework.Utils.Time;

namespace DRN.Test.Unit.Tests.Framework.Utils.Ids;

/// <summary>
/// Verifies the numeric walkthrough example in paper/paper-peerj.md § Numeric Walkthrough.
/// These tests establish the correct hex values that the paper must reference.
/// Uses big-endian byte layout per RFC 9562 V8.
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

    // Sign-bit toggle mask for unsigned↔signed sort-order conversion (RFC 9562 big-endian layout)
    private const uint SignBitToggle = 0x80000000;

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

        // Assert — SKID hex matches paper walkthrough value exactly
        const long paperSkidHex = unchecked((long)0x8BEB_C200_1204_0005UL);
        skid.Should().Be(paperSkidHex,
            "SKID hex must match value stated in paper-peerj.md § Numeric Walkthrough (0x8BEBC20012040005)");
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

        // Extract raw bytes from the GUID in big-endian (RFC 9562 network byte order)
        var guidBytes = skeid.EntityId.ToByteArray(bigEndian: true);

        // Assert — epoch at byte 0
        guidBytes[0].Should().Be(WalkthroughEpoch, "byte 0 should contain epoch");

        // Assert — SKID upper half at bytes 1–4 (big-endian, sign-toggled)
        // Upper half = bits 63–32 of the SKID, XOR'd with 0x80000000 for lexicographic ordering
        var storedUpperHalf = BinaryPrimitives.ReadUInt32BigEndian(guidBytes.AsSpan(1, 4));
        var expectedUpperHalf = (uint)(unchecked((ulong)ExpectedSkid) >> 32);
        var expectedToggled = expectedUpperHalf ^ SignBitToggle;
        storedUpperHalf.Should().Be(expectedToggled,
            $"bytes 1–4 should contain sign-toggled SKID upper half 0x{expectedToggled:X8}");

        // Assert — SKID lower half byte 0 at byte 5
        var expectedLowerHalf = (uint)(ExpectedSkid & 0xFFFFFFFF);
        guidBytes[5].Should().Be((byte)(expectedLowerHalf >> 24),
            "byte 5 should contain SKID lower half MSB");

        // Assert — version marker at byte 6 (RFC 9562 octet 6)
        guidBytes[6].Should().Be(VersionMarker, "byte 6 should contain version marker 0x8D (UUID V8)");

        // Assert — entity type at byte 7
        guidBytes[7].Should().Be(WalkthroughEntityType, "byte 7 should contain entity type");

        // Assert — variant marker at byte 8 (RFC 9562 octet 8)
        guidBytes[8].Should().Be(VariantMarker, "byte 8 should contain variant marker 0x8D (RFC 9562 §4.1)");

        // Assert — SKID lower half bytes 1-3 at bytes 9-11
        guidBytes[9].Should().Be((byte)(expectedLowerHalf >> 16), "byte 9 should contain SKID lower half byte 1");
        guidBytes[10].Should().Be((byte)(expectedLowerHalf >> 8), "byte 10 should contain SKID lower half byte 2");
        guidBytes[11].Should().Be((byte)expectedLowerHalf, "byte 11 should contain SKID lower half byte 3");

        // Assert — MAC bytes at positions 12-15 (contiguous)
        var macByte0 = guidBytes[12];
        var macByte1 = guidBytes[13];
        var macByte2 = guidBytes[14];
        var macByte3 = guidBytes[15];
        // At least one MAC byte should be non-zero (probability of all-zero MAC is ~1/2^32)
        (macByte0 | macByte1 | macByte2 | macByte3).Should().NotBe(0,
            "MAC bytes at positions 12-15 should contain a BLAKE3 keyed MAC");

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

    [Theory]
    [DataInlineUnit]
    public void SKEID_Should_Sort_Lexicographically_In_Chronological_Order(DrnTestContextUnit context)
    {
        var nexusSettings = new NexusAppSettings { AppId = WalkthroughAppId, AppInstanceId = WalkthroughAppInstanceId };
        context.AddToConfiguration(new { NexusAppSettings = nexusSettings });

        var entityIdUtils = context.GetRequiredService<ISourceKnownEntityIdUtils>();

        // Generate SKIDs at different timestamps within the first epoch half
        var earlyBuilder = NumberBuilder.GetLong();
        earlyBuilder.SetResidueValue(1000U); // early timestamp
        earlyBuilder.TryAdd(WalkthroughAppId, 7);
        earlyBuilder.TryAdd(WalkthroughAppInstanceId, 6);
        earlyBuilder.TryAdd(1u, 18);
        var earlySkid = earlyBuilder.GetValue();

        var lateBuilder = NumberBuilder.GetLong();
        lateBuilder.SetResidueValue(2000U); // later timestamp
        lateBuilder.TryAdd(WalkthroughAppId, 7);
        lateBuilder.TryAdd(WalkthroughAppInstanceId, 6);
        lateBuilder.TryAdd(1u, 18);
        var lateSkid = lateBuilder.GetValue();

        // Both first half (negative)
        earlySkid.Should().BeNegative();
        lateSkid.Should().BeNegative();
        earlySkid.Should().BeLessThan(lateSkid, "SKID chronological order");

        var earlySkeid = entityIdUtils.GeneratePlain(earlySkid, WalkthroughEntityType);
        var lateSkeid = entityIdUtils.GeneratePlain(lateSkid, WalkthroughEntityType);

        // SKEID string comparison should match SKID chronological order
        var comparison = string.Compare(earlySkeid.EntityId.ToString(), lateSkeid.EntityId.ToString(), StringComparison.Ordinal);
        comparison.Should().BeNegative("earlier SKEID should sort before later SKEID lexicographically");
    }

    [Theory]
    [DataInlineUnit]
    public void SKEID_Should_Sort_Lexicographically_Across_Epoch_Halves(DrnTestContextUnit context)
    {
        var nexusSettings = new NexusAppSettings { AppId = WalkthroughAppId, AppInstanceId = WalkthroughAppInstanceId };
        context.AddToConfiguration(new { NexusAppSettings = nexusSettings });

        var entityIdUtils = context.GetRequiredService<ISourceKnownEntityIdUtils>();

        // First half SKID (negative, sign=1) — latest timestamp in first half
        var firstHalfBuilder = NumberBuilder.GetLong();
        firstHalfBuilder.SetResidueValue(uint.MaxValue); // max timestamp in first half
        firstHalfBuilder.TryAdd(WalkthroughAppId, 7);
        firstHalfBuilder.TryAdd(WalkthroughAppInstanceId, 6);
        firstHalfBuilder.TryAdd(1u, 18);
        var firstHalfSkid = firstHalfBuilder.GetValue();

        // Second half SKID (positive, sign=0) — earliest timestamp in second half
        var secondHalfBuilder = NumberBuilder.GetLong();
        secondHalfBuilder.MakePositive();
        secondHalfBuilder.SetResidueValue(0U); // earliest timestamp in second half
        secondHalfBuilder.TryAdd(WalkthroughAppId, 7);
        secondHalfBuilder.TryAdd(WalkthroughAppInstanceId, 6);
        secondHalfBuilder.TryAdd(1u, 18);
        var secondHalfSkid = secondHalfBuilder.GetValue();

        firstHalfSkid.Should().BeNegative("first half SKIDs are negative");
        secondHalfSkid.Should().BePositive("second half SKIDs are positive");
        firstHalfSkid.Should().BeLessThan(secondHalfSkid, "signed SKID order: negative < positive");

        var firstHalfSkeid = entityIdUtils.GeneratePlain(firstHalfSkid, WalkthroughEntityType);
        var secondHalfSkeid = entityIdUtils.GeneratePlain(secondHalfSkid, WalkthroughEntityType);

        // Lexicographic comparison of SKEID strings should match signed SKID comparison
        var comparison = string.Compare(firstHalfSkeid.EntityId.ToString(), secondHalfSkeid.EntityId.ToString(), StringComparison.Ordinal);
        comparison.Should().BeNegative(
            "first-half SKEID (earlier epoch half) should sort before second-half SKEID lexicographically, " +
            "matching the signed SKID chronological order");
    }

    [Theory]
    [DataInlineUnit]
    public void SKEID_Batch_Should_Maintain_Lexicographic_Monotonic_Order(DrnTestContextUnit context)
    {
        var nexusSettings = new NexusAppSettings { AppId = WalkthroughAppId, AppInstanceId = WalkthroughAppInstanceId };
        context.AddToConfiguration(new { NexusAppSettings = nexusSettings });
        var entityIdUtils = context.GetRequiredService<ISourceKnownEntityIdUtils>();

        // Generate SKEIDs across both epoch halves with increasing timestamps
        uint[] timestamps = [0U, 100U, 10_000U, 1_000_000U, 100_000_000U, uint.MaxValue - 1, uint.MaxValue];
        var skeidStrings = new List<string>();

        // First half (negative SKIDs, sign=1)
        foreach (var ts in timestamps)
        {
            var builder = NumberBuilder.GetLong();
            builder.SetResidueValue(ts);
            builder.TryAdd(WalkthroughAppId, 7);
            builder.TryAdd(WalkthroughAppInstanceId, 6);
            builder.TryAdd(1u, 18);
            var skeid = entityIdUtils.GeneratePlain(builder.GetValue(), WalkthroughEntityType);
            skeidStrings.Add(skeid.EntityId.ToString());
        }

        // Second half (positive SKIDs, sign=0)
        foreach (var ts in timestamps)
        {
            var builder = NumberBuilder.GetLong();
            builder.MakePositive();
            builder.SetResidueValue(ts);
            builder.TryAdd(WalkthroughAppId, 7);
            builder.TryAdd(WalkthroughAppInstanceId, 6);
            builder.TryAdd(1u, 18);
            var skeid = entityIdUtils.GeneratePlain(builder.GetValue(), WalkthroughEntityType);
            skeidStrings.Add(skeid.EntityId.ToString());
        }

        var sorted = skeidStrings.OrderBy(s => s, StringComparer.Ordinal).ToList();
        sorted.Should().Equal(skeidStrings, "SKEIDs generated in chronological order should already be in lexicographic order");
    }

    [Theory]
    [DataInlineUnit]
    public void SKEID_Should_Preserve_SubTick_Lexicographic_Order(DrnTestContextUnit context)
    {
        var nexusSettings = new NexusAppSettings { AppId = WalkthroughAppId, AppInstanceId = WalkthroughAppInstanceId };
        context.AddToConfiguration(new { NexusAppSettings = nexusSettings });
        var entityIdUtils = context.GetRequiredService<ISourceKnownEntityIdUtils>();

        // Same timestamp, increasing sequence numbers — tests lower-half split ordering
        var skeidStrings = new List<string>();
        for (uint seq = 1; seq <= 10; seq++)
        {
            var builder = NumberBuilder.GetLong();
            builder.SetResidueValue(WalkthroughTimestamp);
            builder.TryAdd(WalkthroughAppId, 7);
            builder.TryAdd(WalkthroughAppInstanceId, 6);
            builder.TryAdd(seq, 18);
            var skeid = entityIdUtils.GeneratePlain(builder.GetValue(), WalkthroughEntityType);
            skeidStrings.Add(skeid.EntityId.ToString());
        }

        var sorted = skeidStrings.OrderBy(s => s, StringComparer.Ordinal).ToList();
        sorted.Should().Equal(skeidStrings, "SKEIDs with same timestamp but increasing sequence should sort lexicographically in sequence order");
    }

    [EntityType(WalkthroughEntityType)]
    class WalkthroughEntity(long id) : SourceKnownEntity(id);
}
