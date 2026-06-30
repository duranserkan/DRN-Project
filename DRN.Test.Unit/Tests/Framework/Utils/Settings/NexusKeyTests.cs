using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using Blake3;
using DRN.Framework.SharedKernel.Json;
using DRN.Framework.Utils.Data.Encryption;
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
        nexusKey.MacKey.Should().BeOfType<SecretKey32>();
        nexusKey.EncryptionKey.Should().BeOfType<SecretKey32>();
        nexusKey.MacKey.Bytes.Should().Equal(DeriveExpectedKey(decodedKeyMaterial, MacKeyDerivationContext));
        nexusKey.EncryptionKey.Bytes.Should().Equal(DeriveExpectedKey(decodedKeyMaterial, EncryptionKeyDerivationContext));
        nexusKey.MacKey.Bytes.Should().NotEqual(nexusKey.EncryptionKey.Bytes);
    }

    [Fact]
    public void SecretKey32_Should_Copy_Key_Material_And_Reject_Wrong_Lengths()
    {
        var source = SequentialKeyBytes.ToArray();
        var key = new SecretKey32(source);
        source[0] = byte.MaxValue;

        key.Length.Should().Be(32);
        key.Span.ToArray().Should().Equal(SequentialKeyBytes);
        key.Memory.ToArray().Should().Equal(SequentialKeyBytes);
        key.Bytes.Should().Equal(SequentialKeyBytes);

        var action = () => { _ = new SecretKey32(new byte[31]); };
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SecretKey32_Should_Clear_And_Reject_Access_After_Dispose()
    {
        var key = new SecretKey32(SequentialKeyBytes);

        key.Dispose();
        var action = () => { _ = key.Bytes; };

        action.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void NexusKey_Should_Dispose_Runtime_Key_Material()
    {
        var nexusKey = new NexusKey(Utf8Key, ByteEncoding.Utf8);
        var macKey = nexusKey.MacKey;
        var encryptionKey = nexusKey.EncryptionKey;

        nexusKey.Dispose();

        var macAction = () => { _ = macKey.Bytes; };
        var encryptionAction = () => { _ = encryptionKey.Bytes; };
        macAction.Should().Throw<ObjectDisposedException>();
        encryptionAction.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void NexusAppSettings_Should_Dispose_Configured_Keys()
    {
        var firstKey = new NexusKey(Utf8Key, ByteEncoding.Utf8) { Default = true };
        var secondKey = new NexusKey(SequentialKeyBytes.Encode(ByteEncoding.Hex), ByteEncoding.Hex);
        var settings = new NexusAppSettings { Keys = [firstKey, secondKey] };

        settings.Dispose();

        var firstAction = () => { _ = firstKey.MacKey.Bytes; };
        var secondAction = () => { _ = secondKey.EncryptionKey.Bytes; };
        firstAction.Should().Throw<ObjectDisposedException>();
        secondAction.Should().Throw<ObjectDisposedException>();
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
            key.MacKey.Bytes.Should().Equal(expectedMacKeyBytes);
            key.EncryptionKey.Bytes.Should().Equal(expectedEncryptionKeyBytes);
            key.MacKey.Bytes.Should().NotEqual(key.EncryptionKey.Bytes);
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
        roundTripped.MacKey.Bytes.Should().Equal(DeriveExpectedKey(SequentialKeyBytes, MacKeyDerivationContext));
    }

    [Fact]
    public void NexusKey_Should_Accept_Utf8_When_ByteCount_Is_Exactly_32()
    {
        var key = new string('a', 30) + "ğ";
        var nexusKey = new NexusKey(key, ByteEncoding.Utf8);

        Encoding.UTF8.GetByteCount(key).Should().Be(32);
        key.Length.Should().NotBe(32);
        nexusKey.MacKey.Bytes.Should().Equal(DeriveExpectedKey(Encoding.UTF8.GetBytes(key), MacKeyDerivationContext));
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
        settings.Keys[0].MacKey.Bytes.Should().Equal(DeriveExpectedKey(SequentialKeyBytes, MacKeyDerivationContext));
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
        settings.Keys[0].MacKey.Bytes.Should().Equal(DeriveExpectedKey(SequentialKeyBytes, MacKeyDerivationContext));
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
