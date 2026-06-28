using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using DRN.Framework.SharedKernel.Json;
using DRN.Framework.Utils.Data.Encodings;

namespace DRN.Test.Unit.Tests.Framework.Utils.Settings;

public class NexusMacKeyTests
{
    private const string Utf8Key = "0123456789abcdef0123456789abcdef";
    private static readonly byte[] SequentialKeyBytes = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

    [Theory]
    [DataMemberUnit(nameof(ValidKeys))]
    public void NexusMacKey_Should_Accept_Only_Valid_32Byte_Key_Formats(string key, ByteEncoding format, string expectedHex)
    {
        var nexusMacKey = new NexusMacKey(key, format) { Default = true };

        nexusMacKey.Format.Should().Be(format);
        nexusMacKey.IsValid.Should().BeTrue();
        Convert.ToHexString(nexusMacKey.KeyAsBinary.ToArray()).Should().Be(expectedHex);
        nexusMacKey.AlternativeKeyAsBinary.Length.Should().Be(32);
    }

    [Fact]
    public void NexusMacKey_Should_Derive_Alternative_Key_From_Decoded_Key_Bytes()
    {
        var keyBytes = Encoding.UTF8.GetBytes(Utf8Key);
        var keys = new[]
        {
            new NexusMacKey(Utf8Key, ByteEncoding.Utf8),
            new NexusMacKey(keyBytes.Encode(ByteEncoding.Hex), ByteEncoding.Hex),
            new NexusMacKey(keyBytes.Encode(ByteEncoding.Base64), ByteEncoding.Base64),
            new NexusMacKey(keyBytes.Encode(ByteEncoding.Base64UrlEncoded), ByteEncoding.Base64UrlEncoded)
        };

        var expectedKeyHash = keys[0].KeyHash;
        var expectedAlternativeKey = keys[0].AlternativeKey;
        var expectedAlternativeKeyBytes = keys[0].AlternativeKeyAsBinary.ToArray();

        foreach (var key in keys)
        {
            key.KeyAsBinary.ToArray().Should().Equal(keyBytes);
            key.KeyHash.Should().Be(expectedKeyHash,
                "derived hash input must be decoded key bytes, not the configured text representation");
            key.AlternativeKey.Should().Be(expectedAlternativeKey,
                "alternative key derivation must be invariant for equivalent raw key bytes");
            key.AlternativeKeyAsBinary.ToArray().Should().Equal(expectedAlternativeKeyBytes);
        }
    }

    [Theory]
    [DataMemberUnit(nameof(InvalidKeys))]
    public void NexusMacKey_Should_Reject_Invalid_Key_Formats(string key, ByteEncoding format)
    {
        var action = () => new NexusMacKey(key, format);

        var exception = action.Should().Throw<ConfigurationException>().Which;
        if (!string.IsNullOrEmpty(key))
            exception.Message.Should().NotContain(key);
    }

    [Theory]
    [DataMemberUnit(nameof(ValidKeys))]
    public void NexusMacKey_Should_RoundTrip_Through_SystemTextJson(string key, ByteEncoding format, string _)
    {
        var settings = new NexusAppSettings
        {
            MacKeys = [new NexusMacKey(key, format) { Default = true }]
        };

        var json = JsonSerializer.Serialize(settings, JsonConventions.DefaultOptions);
        var roundTripped = JsonSerializer.Deserialize<NexusAppSettings>(json, JsonConventions.DefaultOptions);

        roundTripped.Should().NotBeNull();
        roundTripped!.MacKeys.Should().ContainSingle();
        roundTripped.MacKeys[0].Key.Should().Be(key);
        roundTripped.MacKeys[0].Format.Should().Be(format);
        roundTripped.MacKeys[0].Default.Should().BeTrue();
        roundTripped.MacKeys[0].IsValid.Should().BeTrue();
    }

    [Fact]
    public void NexusMacKey_Should_RoundTrip_With_SystemTextJson_DefaultOptions()
    {
        var key = Base64Url.EncodeToString(SequentialKeyBytes);
        var nexusMacKey = new NexusMacKey(key, ByteEncoding.Base64UrlEncoded) { Default = true };

        var json = JsonSerializer.Serialize(nexusMacKey);
        var roundTripped = JsonSerializer.Deserialize<NexusMacKey>(json);

        json.Should().Contain(nameof(ByteEncoding.Base64UrlEncoded));
        roundTripped.Should().NotBeNull();
        roundTripped!.Key.Should().Be(key);
        roundTripped.Format.Should().Be(ByteEncoding.Base64UrlEncoded);
        roundTripped.Default.Should().BeTrue();
        roundTripped.KeyAsBinary.ToArray().Should().Equal(SequentialKeyBytes);
    }

    [Fact]
    public void NexusMacKey_Should_Accept_Utf8_When_ByteCount_Is_Exactly_32()
    {
        var key = new string('a', 30) + "ğ";

        var nexusMacKey = new NexusMacKey(key, ByteEncoding.Utf8);

        Encoding.UTF8.GetByteCount(key).Should().Be(32);
        key.Length.Should().NotBe(32);
        nexusMacKey.KeyAsBinary.ToArray().Should().Equal(Encoding.UTF8.GetBytes(key));
    }

    [Fact]
    public void NexusMacKey_Should_Deserialize_Numeric_Format_With_SystemTextJson()
    {
        var json = $$"""
                     {
                       "macKeys": [
                         {
                           "key": "{{SequentialKeyBytes.Encode(ByteEncoding.Hex)}}",
                           "format": 1,
                           "default": true
                         }
                       ]
                     }
                     """;

        var settings = JsonSerializer.Deserialize<NexusAppSettings>(json, JsonConventions.DefaultOptions);

        settings.Should().NotBeNull();
        settings!.MacKeys.Should().ContainSingle();
        settings.MacKeys[0].Format.Should().Be(ByteEncoding.Hex);
        settings.MacKeys[0].KeyAsBinary.ToArray().Should().Equal(SequentialKeyBytes);
    }

    [Fact]
    public void NexusMacKey_Should_Bind_Format_From_Configuration()
    {
        var key = Base64Url.EncodeToString(SequentialKeyBytes);
        var configuration = new ConfigurationManager()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NexusAppSettings:MacKeys:0:Key"] = key,
                ["NexusAppSettings:MacKeys:0:Format"] = nameof(ByteEncoding.Base64UrlEncoded),
                ["NexusAppSettings:MacKeys:0:Default"] = bool.TrueString
            })
            .Build();

        var settings = configuration.GetSection(nameof(NexusAppSettings)).Get<NexusAppSettings>();

        settings.Should().NotBeNull();
        settings!.Validate();
        settings.MacKeys.Should().ContainSingle();
        settings.MacKeys[0].Format.Should().Be(ByteEncoding.Base64UrlEncoded);
        settings.MacKeys[0].KeyAsBinary.ToArray().Should().Equal(SequentialKeyBytes);
    }

    public static IEnumerable<object[]> ValidKeys()
    {
        yield return
        [
            Utf8Key,
            ByteEncoding.Utf8,
            Convert.ToHexString(Encoding.UTF8.GetBytes(Utf8Key))
        ];
        yield return
        [
            SequentialKeyBytes.Encode(ByteEncoding.Hex),
            ByteEncoding.Hex,
            Convert.ToHexString(SequentialKeyBytes)
        ];
        yield return
        [
            Convert.ToBase64String(SequentialKeyBytes),
            ByteEncoding.Base64,
            Convert.ToHexString(SequentialKeyBytes)
        ];
        yield return
        [
            Base64Url.EncodeToString(SequentialKeyBytes),
            ByteEncoding.Base64UrlEncoded,
            Convert.ToHexString(SequentialKeyBytes)
        ];
    }

    public static IEnumerable<object[]> InvalidKeys()
    {
        yield return ["1019464a0c19475693c155fcb0f7b3e8", ByteEncoding.Hex];
        yield return [Convert.ToBase64String(new byte[16]), ByteEncoding.Base64];
        yield return [Base64Url.EncodeToString(new byte[24]), ByteEncoding.Base64UrlEncoded];
        yield return ["", ByteEncoding.Utf8];
        yield return ["ğ123456789abcdef0123456789abcdef", ByteEncoding.Utf8];
        yield return ["not-a-valid-base64", ByteEncoding.Base64];
    }
}
