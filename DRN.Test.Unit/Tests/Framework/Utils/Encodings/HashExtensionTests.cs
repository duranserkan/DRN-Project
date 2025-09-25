using System.Diagnostics.CodeAnalysis;
using DRN.Framework.Utils.Encodings;

namespace DRN.Test.Unit.Tests.Framework.Utils.Encodings;

//todo test default hash behavior
public class HashExtensionTests
{
    private const string HelloWorld = "Hello World";
    private const string HelloWorldKey = "HelloWorldHelloWorldHelloWorld12"; //32 Character

    [Theory]
    [SuppressMessage("ReSharper", "RedundantArgumentDefaultValue")]
    [InlineData(HashAlgorithm.XxHash3_64, "E34615AADE2E6333")]
    [InlineData(HashAlgorithm.XxHash64, "6334D20719245BC2")]
    [InlineData(HashAlgorithm.XxHash128, "403383A15C99BEEE9AC3AF2126A00292")]
    [InlineData(HashAlgorithm.Blake3, "41F8394111EB713A22165C46C90AB8F0FD9399C92028FD6D288944B23FF5BF76")]
    [InlineData(HashAlgorithm.Sha256, "A591A6D40BF420404A011733CFB7B190D62C65BF0BCDA32B57B277D9AD9F146E")]
    [InlineData(HashAlgorithm.Sha512, "2C74FD17EDAFD80E8447B0D46741EE243B7EB74DD2149A0AB1B9246FB30382F27E853D8585719E0E67CBDA0DAA8F51671064615D645AE27ACB15BFB1447F459B")]
    public void String_Should_Be_Hashed_With_Different_Algorithms_And_Encodings(HashAlgorithm algorithm, string expectedHashHex)
    {
        var hashHex = HelloWorld.Hash(algorithm, ByteEncoding.Hex);
        hashHex.Should().Be(expectedHashHex);

        new BinaryData(HelloWorld).ToArray().Hash(algorithm, ByteEncoding.Hex).Should().Be(expectedHashHex);
        new BinaryData(HelloWorld).ToMemory().Hash(algorithm, ByteEncoding.Hex).Should().Be(expectedHashHex);

        var expectedBase64Hash = hashHex.Decode(ByteEncoding.Hex).Encode(ByteEncoding.Base64);
        var base64Hash = HelloWorld.Hash(algorithm, ByteEncoding.Base64);
        base64Hash.Should().Be(expectedBase64Hash);

        var expectedBase64UrlHash = hashHex.Decode(ByteEncoding.Hex).Encode(ByteEncoding.Base64UrlEncoded);
        var base64UrlHash = HelloWorld.Hash(algorithm, ByteEncoding.Base64UrlEncoded);
        base64UrlHash.Should().Be(expectedBase64UrlHash);

        var base64UrlHashDefaultEncoding = HelloWorld.Hash(algorithm);
        base64UrlHash.Should().Be(base64UrlHashDefaultEncoding);
    }

    [Fact]
    public void String_Should_Be_Hashed_With_Blake3_With_Key()
    {
        var helloWorldBinary = BinaryData.FromString(HelloWorld);
        var helloWorldKeyBinary = BinaryData.FromString(HelloWorldKey);
        var expectedHashHex = "93375BB53355A53141278C5675C49B0AE029CF9852C94933DAFCEEEE8766B6B8";

        var hashHex = HelloWorld.HashWithKey(helloWorldKeyBinary, SecureHashAlgorithm.Blake3With32CharKey, ByteEncoding.Hex);
        hashHex.Should().Be(expectedHashHex);

        hashHex = helloWorldBinary.ToArray().HashWithKey(helloWorldKeyBinary, SecureHashAlgorithm.Blake3With32CharKey, ByteEncoding.Hex);
        hashHex.Should().Be(expectedHashHex);

        hashHex = helloWorldBinary.ToMemory().HashWithKey(helloWorldKeyBinary, SecureHashAlgorithm.Blake3With32CharKey, ByteEncoding.Hex);
        hashHex.Should().Be(expectedHashHex);
    }
}