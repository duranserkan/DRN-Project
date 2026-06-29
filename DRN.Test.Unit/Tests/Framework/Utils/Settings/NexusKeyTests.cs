using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using Blake3;
using DRN.Framework.SharedKernel.Json;
using DRN.Framework.Utils.Data.Encodings;

namespace DRN.Test.Unit.Tests.Framework.Utils.Settings;

public class NexusKeyTests
{
    private const string Utf8Key = "0123456789abcdef0123456789abcdef";
    private const string MacKeyDerivationContext = "DRN.Framework.Utils NexusKey 1881 1919 1923 193∞ derive_key mackey 2026-06-29 21:57:43 v1";
    private const string EncryptionKeyDerivationContext = "DRN.Framework.Utils NexusKey 1881 1919 1923 193∞ derive_key encryption key 2026-06-29 21:57:43 v1";
    private static readonly byte[] SequentialKeyBytes = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

    [Theory]
    [DataMemberUnit(nameof(ValidKeys))]
    public void NexusKey_Should_Accept_Only_Valid_32Byte_Key_Formats(string key, ByteEncoding format, string expectedDecodedHex)
    {
        var decodedKeyMaterial = Convert.FromHexString(expectedDecodedHex);
        var nexusKey = new NexusKey(key, format) { Default = true };

        nexusKey.Format.Should().Be(format);
        nexusKey.IsValid.Should().BeTrue();
        nexusKey.MacKey.ToArray().Should().Equal(DeriveExpectedKey(decodedKeyMaterial, MacKeyDerivationContext));
        nexusKey.EncryptionKey.ToArray().Should().Equal(DeriveExpectedKey(decodedKeyMaterial, EncryptionKeyDerivationContext));
        nexusKey.MacKey.ToArray().Should().NotEqual(nexusKey.EncryptionKey.ToArray());
    }

    [Fact]
    public void NexusKey_Should_Derive_MacKey_And_EncryptionKey_From_Decoded_Key_Bytes()
    {
        var keyBytes = Encoding.UTF8.GetBytes(Utf8Key);
        var expectedMacKeyBytes = DeriveExpectedKey(keyBytes, MacKeyDerivationContext);
        var expectedEncryptionKeyBytes = DeriveExpectedKey(keyBytes, EncryptionKeyDerivationContext);
        var keys = new[]
        {
            new NexusKey(Utf8Key, ByteEncoding.Utf8),
            new NexusKey(keyBytes.Encode(ByteEncoding.Hex), ByteEncoding.Hex),
            new NexusKey(keyBytes.Encode(ByteEncoding.Base64), ByteEncoding.Base64),
            new NexusKey(keyBytes.Encode(ByteEncoding.Base64UrlEncoded), ByteEncoding.Base64UrlEncoded)
        };

        foreach (var key in keys)
        {
            key.MacKey.Length.Should().Be(32);
            key.EncryptionKey.Length.Should().Be(32);
            key.MacKey.ToArray().Should().Equal(expectedMacKeyBytes);
            key.EncryptionKey.ToArray().Should().Equal(expectedEncryptionKeyBytes);
            key.MacKey.ToArray().Should().NotEqual(key.EncryptionKey.ToArray());
        }
    }

    [Theory]
    [DataMemberUnit(nameof(InvalidKeys))]
    public void NexusKey_Should_Reject_Invalid_Key_Formats(string key, ByteEncoding format)
    {
        var action = () => new NexusKey(key, format);

        var exception = action.Should().Throw<ConfigurationException>().Which;
        if (!string.IsNullOrEmpty(key))
            exception.Message.Should().NotContain(key);
    }

    [Theory]
    [DataMemberUnit(nameof(ValidKeys))]
    public void NexusKey_Should_RoundTrip_Through_SystemTextJson(string key, ByteEncoding format, string _)
    {
        var settings = new NexusAppSettings
        {
            Keys = [new NexusKey(key, format) { Default = true }]
        };

        var json = JsonSerializer.Serialize(settings, JsonConventions.DefaultOptions);
        var roundTripped = JsonSerializer.Deserialize<NexusAppSettings>(json, JsonConventions.DefaultOptions);

        roundTripped.Should().NotBeNull();
        roundTripped.Keys.Should().ContainSingle();
        roundTripped.Keys[0].KeyMaterial.Should().Be(key);
        roundTripped.Keys[0].Format.Should().Be(format);
        roundTripped.Keys[0].Default.Should().BeTrue();
        roundTripped.Keys[0].IsValid.Should().BeTrue();
    }

    [Fact]
    public void NexusKey_Should_RoundTrip_With_SystemTextJson_DefaultOptions()
    {
        var key = Base64Url.EncodeToString(SequentialKeyBytes);
        var nexusKey = new NexusKey(key, ByteEncoding.Base64UrlEncoded) { Default = true };

        var json = JsonSerializer.Serialize(nexusKey);
        var roundTripped = JsonSerializer.Deserialize<NexusKey>(json);

        json.Should().Contain(nameof(ByteEncoding.Base64UrlEncoded));
        roundTripped.Should().NotBeNull();
        roundTripped.KeyMaterial.Should().Be(key);
        roundTripped.Format.Should().Be(ByteEncoding.Base64UrlEncoded);
        roundTripped.Default.Should().BeTrue();
        roundTripped.MacKey.ToArray().Should().Equal(DeriveExpectedKey(SequentialKeyBytes, MacKeyDerivationContext));
    }

    [Fact]
    public void NexusKey_Should_Accept_Utf8_When_ByteCount_Is_Exactly_32()
    {
        var key = new string('a', 30) + "ğ";
        var nexusKey = new NexusKey(key, ByteEncoding.Utf8);

        Encoding.UTF8.GetByteCount(key).Should().Be(32);
        key.Length.Should().NotBe(32);
        nexusKey.MacKey.ToArray().Should().Equal(DeriveExpectedKey(Encoding.UTF8.GetBytes(key), MacKeyDerivationContext));
    }

    [Fact]
    public void NexusKey_Should_Deserialize_Numeric_Format_With_SystemTextJson()
    {
        var json = $$"""
                     {
                       "keys": [
                         {
                           "keyMaterial": "{{SequentialKeyBytes.Encode(ByteEncoding.Hex)}}",
                           "format": 1,
                           "default": true
                         }
                       ]
                     }
                     """;

        var settings = JsonSerializer.Deserialize<NexusAppSettings>(json, JsonConventions.DefaultOptions);

        settings.Should().NotBeNull();
        settings.Keys.Should().ContainSingle();
        settings.Keys[0].Format.Should().Be(ByteEncoding.Hex);
        settings.Keys[0].MacKey.ToArray().Should().Equal(DeriveExpectedKey(SequentialKeyBytes, MacKeyDerivationContext));
    }

    [Fact]
    public void NexusKey_Should_Bind_Format_From_Configuration()
    {
        var key = Base64Url.EncodeToString(SequentialKeyBytes);
        var configuration = new ConfigurationManager()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NexusAppSettings:Keys:0:KeyMaterial"] = key,
                ["NexusAppSettings:Keys:0:Format"] = nameof(ByteEncoding.Base64UrlEncoded),
                ["NexusAppSettings:Keys:0:Default"] = bool.TrueString
            })
            .Build();

        var settings = configuration.GetSection(nameof(NexusAppSettings)).Get<NexusAppSettings>();

        settings.Should().NotBeNull();
        settings.Validate();
        settings.Keys.Should().ContainSingle();
        settings.Keys[0].Format.Should().Be(ByteEncoding.Base64UrlEncoded);
        settings.Keys[0].MacKey.ToArray().Should().Equal(DeriveExpectedKey(SequentialKeyBytes, MacKeyDerivationContext));
    }

    private static byte[] DeriveExpectedKey(ReadOnlySpan<byte> keyMaterial, string context)
    {
        var derived = new byte[32];
        using var hasher = Hasher.NewDeriveKey(context);
        hasher.Update(keyMaterial);
        hasher.Finalize(derived);
        return derived;
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
