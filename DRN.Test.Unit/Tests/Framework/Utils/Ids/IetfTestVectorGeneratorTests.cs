using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using Blake3;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Data.Encodings;
using DRN.Framework.Utils.Ids;
using DRN.Framework.Utils.Numbers;
using DRN.Framework.Utils.Time;

namespace DRN.Test.Unit.Tests.Framework.Utils.Ids;

/// <summary>
/// Generates and asserts concrete test vectors for the IETF SKID Internet Draft (docs/draft-skid-00.md, Appendix A).
/// Uses well-known IETF-standard test keys (sequential bytes 0x00..0x1F) for reproducibility.
/// Any change to key derivation, BLAKE3 MAC, or AES encryption that would invalidate the document's
/// Appendix A hex values will cause these assertions to fail.
/// </summary>
public class IetfTestVectorGeneratorTests(ITestOutputHelper output)
{
    private const string TestMacKeyHex = "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F";
    private const string TestAesKeyHex = "3E7E55BEC606C85A816104A7BB9A7E490E957DBADD3886402EBBD4CAE0E45B3D";

    private const ulong TestSkidBits = 0x881B3200050C002AUL;
    private const long TestSkidDecimal = -8639256484514103254L;
    private const string TestSkidHex = "0x881B3200050C002A";

    private const uint TestSignBitToggle = 0x80000000;
    private const byte TestEpochByte = 0x00;
    private const byte TestMarkerByte = 0x8D;
    private const byte TestEntityType = 0x01;
    private const byte TestAppId = 5;
    private const byte TestAppInstanceId = 3;
    private const uint TestSequenceId = 42;
    private const uint TestTimestampTicks = 272000000;

    private const string TestMacHex = "6279D160";
    private const string TestPlainHex = "00081B3200058D018D0C002A6279D160";
    private const string TestPlainGuid = "00081b32-0005-8d01-8d0c-002a6279d160";
    private const string TestSecureHex = "785D100D4AE356DCA7E68B350566441D";
    private const string TestSecureGuid = "785d100d-4ae3-56dc-a7e6-8b350566441d";

    [Fact]
    public void Appendix_A_Recipe_Should_Match_Published_Skeid_And_Secure_Skeid()
    {
        var macKey = Convert.FromHexString(TestMacKeyHex);
        var aesKey = Convert.FromHexString(TestAesKeyHex);

        var upperHalf = (uint)(TestSkidBits >> 32);
        var lowerHalf = (uint)(TestSkidBits & uint.MaxValue);

        Span<byte> skeidBytes = stackalloc byte[16];
        skeidBytes.Clear();
        skeidBytes[0] = TestEpochByte;
        BinaryPrimitives.WriteUInt32BigEndian(skeidBytes[1..5], upperHalf ^ TestSignBitToggle);
        skeidBytes[5] = (byte)(lowerHalf >> 24);
        skeidBytes[6] = TestMarkerByte;
        skeidBytes[7] = TestEntityType;
        skeidBytes[8] = TestMarkerByte;
        skeidBytes[9] = (byte)(lowerHalf >> 16);
        skeidBytes[10] = (byte)(lowerHalf >> 8);
        skeidBytes[11] = (byte)lowerHalf;

        Span<byte> macBytes = stackalloc byte[4];
        using var hasher = Hasher.NewKeyed(BinaryData.FromBytes(macKey));
        hasher.Update(skeidBytes);
        hasher.Finalize(macBytes);
        macBytes.CopyTo(skeidBytes[12..]);

        Convert.ToHexString(macBytes).Should()
            .Be(TestMacHex, "Appendix A.3 MAC bytes are the first four BLAKE3 keyed output bytes");

        var plainBytes = skeidBytes.ToArray();
        Convert.ToHexString(plainBytes).Should()
            .Be(TestPlainHex, "plain SKEID bytes must match draft-skid-00.md Appendix A.3");

        new Guid(plainBytes, bigEndian: true).ToString().Should()
            .Be(TestPlainGuid, "plain SKEID GUID must match draft-skid-00.md Appendix A.3");

        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = aesKey;

        Span<byte> secureBytes = stackalloc byte[16];
        aes.EncryptEcb(plainBytes, secureBytes, PaddingMode.None);

        Convert.ToHexString(secureBytes).Should()
            .Be(TestSecureHex, "AES-256-ECB ciphertext must match draft-skid-00.md Appendix A.4");

        new Guid(secureBytes, bigEndian: true).ToString().Should()
            .Be(TestSecureGuid, "secure SKEID GUID must match draft-skid-00.md Appendix A.4");
    }

    [Theory]
    [DataInlineUnit]
    public void Generate_IETF_Test_Vectors(DrnTestContextUnit context)
    {
        var nexusMacKey = new NexusMacKey(TestMacKeyHex, ByteEncoding.Hex) { Default = true };
        var nexusSettings = new NexusAppSettings
        {
            AppId = TestAppId,
            AppInstanceId = TestAppInstanceId,
            MacKeys = [nexusMacKey]
        };
        context.AddToConfiguration(new { NexusAppSettings = nexusSettings });

        var idUtils = context.GetRequiredService<ISourceKnownIdUtils>();
        var entityIdUtils = context.GetRequiredService<ISourceKnownEntityIdUtils>();

        // --- A.1: Test Key Material ---
        var macKeyBinary = nexusMacKey.KeyAsBinary;
        var aesKeyBinary = nexusMacKey.AlternativeKeyAsBinary;
        var epochText = EpochTimeUtils.DefaultEpoch
            .ToUniversalTime()
            .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        // Assert key derivation matches draft-skid-00.md Appendix A.1
        nexusMacKey.Format.Should()
            .Be(ByteEncoding.Hex, "Appendix A.1 defines the canonical MAC key as hexadecimal octets");
        Convert.ToHexString(macKeyBinary.ToArray()).Should()
            .Be(TestMacKeyHex, "MAC key hex must match Appendix A.1");
        Convert.ToHexString(aesKeyBinary.ToArray()).Should()
            .Be(TestAesKeyHex, "AES key hex must match Appendix A.1 (derived from MAC key via hash chain)");
        epochText.Should()
            .Be("2025-01-01T00:00:00Z", "default epoch must match Appendix A.1");

        output.WriteLine($"""
            === A.1. Test Key Material ===

            MAC Key (32 bytes, hex):   {Convert.ToHexString(macKeyBinary.ToArray())}
            AES Key (32 bytes, hex):   {Convert.ToHexString(aesKeyBinary.ToArray())}
            Epoch:                     {epochText}

            """);

        // --- A.2: SKID Generation ---
        // Construct SKID manually from known fields:
        //   sign=1, timestamp=272000000 (250ms ticks), appId=5, appInstanceId=3, sequenceId=42
        var builder = NumberBuilder.GetLong();

        // Sign bit = 1 (default, first half epoch) → set timestamp as unsigned 32-bit residue
        // Timestamp: 68000000 seconds × 4 ticks/s = 272,000,000 ticks
        builder.SetResidueValue(TestTimestampTicks);
        builder.TryAdd(TestAppId, 7);   // appId
        builder.TryAdd(TestAppInstanceId, 6);   // appInstanceId
        builder.TryAdd(TestSequenceId, 18); // sequenceId (18 bits)

        var skid = builder.GetValue();

        // Assert SKID matches expected value for new layout
        var skidHex = $"0x{(ulong)skid:X16}";
        skid.Should().Be(TestSkidDecimal, "SKID decimal must match draft-skid-00.md Appendix A.2");
        skidHex.Should().Be(TestSkidHex, "SKID hex must match draft-skid-00.md Appendix A.2");

        var parsed = idUtils.Parse(skid);

        output.WriteLine($"""
            === A.2. SKID Generation ===

            Input:
              entityType     = {TestEntityType}
              appId          = {TestAppId}
              appInstanceId  = {TestAppInstanceId}
              timestamp      = {TestTimestampTicks} (250ms ticks since epoch = 68000000 seconds)
              sequenceId     = {TestSequenceId}

            SKID (decimal):    {skid}
            SKID (hex):        0x{(ulong)skid:X16}

            Parsed fields:
              AppId:           {parsed.AppId}
              AppInstanceId:   {parsed.AppInstanceId}
              SequenceId:      {parsed.InstanceId}
              CreatedAt:       {parsed.CreatedAt:O}

            """);

        parsed.AppId.Should().Be(TestAppId);
        parsed.AppInstanceId.Should().Be(TestAppInstanceId);
        parsed.InstanceId.Should().Be(TestSequenceId);

        // --- A.3: SKEID Generation (Plain) ---
        var plainId = entityIdUtils.GeneratePlain(skid, TestEntityType);
        var plainBytes = plainId.EntityId.ToByteArray(bigEndian: true);

        // Assert exact byte layout matches draft-skid-00.md Appendix A.3
        var plainHex = Convert.ToHexString(plainBytes);
        var plainGuidStr = plainId.EntityId.ToString();
        plainGuidStr.Should().Be(TestPlainGuid, "plain SKEID GUID must match draft-skid-00.md Appendix A.3");
        plainHex.Should().Be(TestPlainHex, "plain SKEID hex must match draft-skid-00.md Appendix A.3");

        // Structural assertions — SKEID big-endian byte layout (RFC 9562)
        plainBytes[0].Should().Be(TestEpochByte, "byte 0 = epoch 0x00");
        plainBytes[7].Should().Be(TestEntityType, "byte 7 = entity type");
        plainBytes[6].Should().Be(TestMarkerByte, "byte 6 = version marker");
        plainBytes[8].Should().Be(TestMarkerByte, "byte 8 = variant marker");

        var byteLayout = string.Join("\n", Enumerable.Range(0, 16)
            .Select(i => $"  Byte {i,2}: 0x{plainBytes[i]:X2}  ({DescribeByte(i)})"));

        output.WriteLine($"""
            === A.3. SKEID Generation (Plain) ===

            Input:
              SKID = {skid}
              entityType = {TestEntityType}

            Byte layout:
            {byteLayout}

            GUID:              {plainId.EntityId}
            Hex (all bytes):   {Convert.ToHexString(plainBytes)}

            """);

        // Verify markers (RFC 9562 big-endian positions)
        plainBytes[6].Should().Be(TestMarkerByte, "marker version at RFC octet 6");
        plainBytes[8].Should().Be(TestMarkerByte, "marker variant at RFC octet 8");

        // --- A.4: Secure SKEID Generation ---
        var secureId = entityIdUtils.GenerateSecure(skid, TestEntityType);
        var secureBytes = secureId.EntityId.ToByteArray(bigEndian: true);

        // Assert exact ciphertext
        var secureGuidStr = secureId.EntityId.ToString();
        var secureHex = Convert.ToHexString(secureBytes);
        secureGuidStr.Should().Be(TestSecureGuid, "secure SKEID GUID must match draft-skid-00.md Appendix A.4");
        secureHex.Should().Be(TestSecureHex, "secure SKEID hex must match draft-skid-00.md Appendix A.4");

        output.WriteLine($"""
            === A.4. Secure SKEID Generation ===

            Input:
              SKEID from A.3 (plaintext bytes)
              aesKey = [see A.1]

            GUID:              {secureId.EntityId}
            Hex (all bytes):   {Convert.ToHexString(secureBytes)}

            """);

        // --- A.5: Round-Trip Verification ---
        var parsedPlain = entityIdUtils.Parse(plainId.EntityId);
        parsedPlain.Valid.Should().BeTrue();
        parsedPlain.Source.Id.Should().Be(skid);
        parsedPlain.Secure.Should().BeFalse();

        var parsedSecure = entityIdUtils.Parse(secureId.EntityId);
        parsedSecure.Valid.Should().BeTrue();
        parsedSecure.Source.Id.Should().Be(skid);
        parsedSecure.Secure.Should().BeTrue();

        var converted = entityIdUtils.ToSecure(parsedPlain);
        converted.EntityId.Should().Be(secureId.EntityId);

        var backToPlain = entityIdUtils.ToPlain(parsedSecure);
        backToPlain.EntityId.Should().Be(plainId.EntityId);

        output.WriteLine($"""
            === A.5. Round-Trip Verification ===

            1. Parse SKEID:          Valid={parsedPlain.Valid}, SKID={parsedPlain.Source.Id} ✓
            2. Parse Secure SKEID:   Valid={parsedSecure.Valid}, SKID={parsedSecure.Source.Id} ✓
            3. ToSecure(SKEID):      GUID={converted.EntityId} == Secure ✓
            4. ToPlain(Secure):   GUID={backToPlain.EntityId} == Plain ✓

            All round-trip verifications passed.
            """);
    }

    private static string DescribeByte(int index) => index switch
    {
        0 => "Epoch",
        1 or 2 or 3 or 4 => "ID upper half (sign-toggled, big-endian)",
        5 => "ID lower byte 0 (MSB)",
        6 => "Marker Version (0x8D) — RFC 9562 octet 6",
        7 => "Entity Type",
        8 => "Marker Variant (0x8D) — RFC 9562 octet 8",
        9 or 10 or 11 => "ID lower bytes 1-3",
        12 or 13 or 14 or 15 => "MAC (BLAKE3 keyed)",
        _ => "unknown"
    };

    [EntityType(TestEntityType)]
    private class TestVectorEntity(long id) : SourceKnownEntity(id);
}
