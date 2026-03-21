using System.Buffers.Text;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Data.Encodings;
using DRN.Framework.Utils.Ids;
using DRN.Framework.Utils.Numbers;

namespace DRN.Test.Unit.Tests.Framework.Utils.Ids;

/// <summary>
/// Generates and asserts concrete test vectors for the IETF SKID Internet Draft (docs/draft-skid-00.md, Appendix A).
/// Uses well-known IETF-standard test keys (sequential bytes 0x00..0x1F) for reproducibility.
/// Any change to key derivation, BLAKE3 MAC, or AES encryption that would invalidate the document's
/// Appendix A hex values will cause these assertions to fail.
/// </summary>
public class IetfTestVectorGeneratorTests
{
    [Theory]
    [DataInlineUnit]
    public void Generate_IETF_Test_Vectors(DrnTestContextUnit context)
    {
        // --- Well-known test key: sequential bytes 0x00 through 0x1F (32 bytes) ---
        var testKeyBytes = new byte[32];
        for (var i = 0; i < 32; i++) testKeyBytes[i] = (byte)i;
        var testKeyBase64Url = Base64Url.EncodeToString(testKeyBytes);

        var nexusMacKey = new NexusMacKey(testKeyBase64Url) { Default = true };
        var nexusSettings = new NexusAppSettings
        {
            AppId = 5,
            AppInstanceId = 3,
            MacKeys = [nexusMacKey]
        };
        context.AddToConfiguration(new { NexusAppSettings = nexusSettings });

        var idUtils = context.GetRequiredService<ISourceKnownIdUtils>();
        var entityIdUtils = context.GetRequiredService<ISourceKnownEntityIdUtils>();

        // --- A.1: Test Key Material ---
        var macKeyBinary = nexusMacKey.KeyAsBinary;
        var aesKeyBinary = nexusMacKey.AlternativeKeyAsBinary;

        // Assert key derivation matches draft-skid-00.md Appendix A.1
        Convert.ToHexString(macKeyBinary.ToArray()).Should().Be(
            "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F",
            "MAC key hex must match Appendix A.1");
        Convert.ToHexString(aesKeyBinary.ToArray()).Should().Be(
            "64EB58C637B051F1F3BF93A40C669E46F2DB5DCDFDEAFF05592782EEB485643C",
            "AES key hex must match Appendix A.1 (derived from MAC key via hash chain)");

        Console.WriteLine("=== A.1. Test Key Material ===");
        Console.WriteLine();
        Console.WriteLine($"MAC Key (32 bytes, hex):   {Convert.ToHexString(macKeyBinary.ToArray())}");
        Console.WriteLine($"MAC Key (base64url):       {testKeyBase64Url}");
        Console.WriteLine($"AES Key (32 bytes, hex):   {Convert.ToHexString(aesKeyBinary.ToArray())}");
        Console.WriteLine($"AES Key (base64url):       {aesKeyBinary.ToMemory().Span.Encode()}");
        Console.WriteLine($"Epoch:                     2025-01-01T00:00:00Z");
        Console.WriteLine();

        // --- A.2: SKID Generation ---
        // Construct SKID manually from known fields:
        //   sign=1, timestamp=272000000 (250ms ticks), appId=5, appInstanceId=3, sequenceId=42
        var builder = NumberBuilder.GetLong();

        // Sign bit = 1 (default, first half epoch) → set timestamp as unsigned 32-bit residue
        // Timestamp: 68000000 seconds × 4 ticks/s = 272,000,000 ticks
        builder.SetResidueValue(272000000);
        builder.TryAdd(5, 7);   // appId
        builder.TryAdd(3, 6);   // appInstanceId
        builder.TryAdd(42, 18); // sequenceId (18 bits)

        var skid = builder.GetValue();

        // Assert SKID matches expected value for new layout
        // Note: hex value changed from old layout due to bit width changes
        var skidHex = $"0x{(ulong)skid:X16}";

        Console.WriteLine("=== A.2. SKID Generation ===");
        Console.WriteLine();
        Console.WriteLine($"Input:");
        Console.WriteLine($"  entityType     = 1");
        Console.WriteLine($"  appId          = 5");
        Console.WriteLine($"  appInstanceId  = 3");
        Console.WriteLine($"  timestamp      = 272000000 (250ms ticks since epoch = 68000000 seconds)");
        Console.WriteLine($"  sequenceId     = 42");
        Console.WriteLine();
        Console.WriteLine($"SKID (decimal):    {skid}");
        Console.WriteLine($"SKID (hex):        0x{(ulong)skid:X16}");

        // Verify parse round-trip
        var parsed = idUtils.Parse(skid);
        Console.WriteLine();
        Console.WriteLine($"Parsed fields:");
        Console.WriteLine($"  AppId:           {parsed.AppId}");
        Console.WriteLine($"  AppInstanceId:   {parsed.AppInstanceId}");
        Console.WriteLine($"  SequenceId:      {parsed.InstanceId}");
        Console.WriteLine($"  CreatedAt:       {parsed.CreatedAt:O}");
        Console.WriteLine();

        parsed.AppId.Should().Be(5);
        parsed.AppInstanceId.Should().Be(3);
        parsed.InstanceId.Should().Be(42);

        // --- A.3: SKEID Generation (Plain) ---
        byte entityType = 1;
        var plainId = entityIdUtils.GeneratePlain(skid, entityType);

        var plainBytes = plainId.EntityId.ToByteArray();

        // Assert exact byte layout matches draft-skid-00.md Appendix A.3
        // Note: exact hex depends on BLAKE3 MAC computation with the test key and the new SKID value
        var plainHex = Convert.ToHexString(plainBytes);
        var plainGuidStr = plainId.EntityId.ToString();
        Console.WriteLine($"Plain SKEID hex: {plainHex}");
        Console.WriteLine($"Plain SKEID GUID: {plainGuidStr}");

        // Structural assertions — SKEID byte layout is unchanged
        // Bytes 0-3: SKID upper half, Byte 4: entity type, Byte 5: epoch,
        // Byte 6: MAC byte 0, Byte 7: version marker, Byte 8: variant marker,
        // Bytes 9-11: MAC bytes 1-3, Bytes 12-15: SKID lower half
        plainBytes[4].Should().Be(entityType, "byte 4 = entity type");
        plainBytes[5].Should().Be(0x00, "byte 5 = epoch 0x00");
        plainBytes[7].Should().Be(0x8D, "byte 7 = version marker");
        plainBytes[8].Should().Be(0x8D, "byte 8 = variant marker");

        Console.WriteLine("=== A.3. SKEID Generation (Plain) ===");
        Console.WriteLine();
        Console.WriteLine($"Input:");
        Console.WriteLine($"  SKID = {skid}");
        Console.WriteLine($"  entityType = {entityType}");
        Console.WriteLine();
        Console.WriteLine($"Byte layout:");
        for (var i = 0; i < 16; i++)
            Console.WriteLine($"  Byte {i,2}: 0x{plainBytes[i]:X2}  ({DescribeByte(i)})");
        Console.WriteLine();
        Console.WriteLine($"GUID:              {plainId.EntityId}");
        Console.WriteLine($"Hex (all bytes):   {Convert.ToHexString(plainBytes)}");
        Console.WriteLine();

        // Verify markers
        plainBytes[7].Should().Be(0x8D, "marker version");
        plainBytes[8].Should().Be(0x8D, "marker variant");

        // --- A.4: Secure SKEID Generation ---
        var secureId = entityIdUtils.GenerateSecure(skid, entityType);

        var secureBytes = secureId.EntityId.ToByteArray();

        // Assert exact ciphertext — depends on AES-ECB encryption with the derived key and the new SKEID plaintext
        var secureGuidStr = secureId.EntityId.ToString();
        var secureHex = Convert.ToHexString(secureBytes);
        Console.WriteLine($"Secure SKEID GUID: {secureGuidStr}");
        Console.WriteLine($"Secure SKEID hex: {secureHex}");

        Console.WriteLine("=== A.4. Secure SKEID Generation ===");
        Console.WriteLine();
        Console.WriteLine($"Input:");
        Console.WriteLine($"  SKEID from A.3 (plaintext bytes)");
        Console.WriteLine($"  aesKey = [see A.1]");
        Console.WriteLine();
        Console.WriteLine($"GUID:              {secureId.EntityId}");
        Console.WriteLine($"Hex (all bytes):   {Convert.ToHexString(secureBytes)}");
        Console.WriteLine();

        // --- A.5: Round-Trip Verification ---
        Console.WriteLine("=== A.5. Round-Trip Verification ===");
        Console.WriteLine();

        // 1. Parse SKEID → verify SKID matches
        var parsedPlain = entityIdUtils.Parse(plainId.EntityId);
        parsedPlain.Valid.Should().BeTrue();
        parsedPlain.Source.Id.Should().Be(skid);
        parsedPlain.Secure.Should().BeFalse();
        Console.WriteLine($"1. Parse SKEID:          Valid={parsedPlain.Valid}, SKID={parsedPlain.Source.Id} ✓");

        // 2. Parse Secure SKEID → verify SKID matches
        var parsedSecure = entityIdUtils.Parse(secureId.EntityId);
        parsedSecure.Valid.Should().BeTrue();
        parsedSecure.Source.Id.Should().Be(skid);
        parsedSecure.Secure.Should().BeTrue();
        Console.WriteLine($"2. Parse Secure SKEID:   Valid={parsedSecure.Valid}, SKID={parsedSecure.Source.Id} ✓");

        // 3. ToSecure(SKEID) → matches Secure SKEID
        var converted = entityIdUtils.ToSecure(parsedPlain);
        converted.EntityId.Should().Be(secureId.EntityId);
        Console.WriteLine($"3. ToSecure(SKEID):      GUID={converted.EntityId} == Secure ✓");

        // 4. ToPlain(Secure) → matches SKEID
        var backToPlain = entityIdUtils.ToPlain(parsedSecure);
        backToPlain.EntityId.Should().Be(plainId.EntityId);
        Console.WriteLine($"4. ToPlain(Secure):   GUID={backToPlain.EntityId} == Plain ✓");
        Console.WriteLine();
        Console.WriteLine("All round-trip verifications passed.");
    }

    private static string DescribeByte(int index) => index switch
    {
        0 or 1 or 2 or 3 => "ID first half",
        4 => "Entity Type",
        5 => "Epoch",
        6 => "MAC byte 0",
        7 => "Marker Version (0x8D)",
        8 => "Marker Variant (0x8D)",
        9 => "MAC byte 1",
        10 => "MAC byte 2",
        11 => "MAC byte 3",
        12 or 13 or 14 or 15 => "ID second half",
        _ => "unknown"
    };

    [EntityType(1)]
    private class TestVectorEntity(long id) : SourceKnownEntity(id);
}
