using System.Text;
using DRN.Framework.Utils.Data.Encodings;

namespace DRN.Test.Unit.Tests.Framework.Utils.Encodings;

public class Base32EncodingTests
{
    // RFC 4648 section 10 publishes these exact padded Base32 input/output pairs.
    // The plain-text inputs contain only US-ASCII characters and are converted to their octets before encoding.
    // Reference: https://www.rfc-editor.org/rfc/rfc4648.html#section-10
    [Theory]
    [DataInlineUnit("", "")]
    [DataInlineUnit("f", "MY======")]
    [DataInlineUnit("fo", "MZXQ====")]
    [DataInlineUnit("foo", "MZXW6===")]
    [DataInlineUnit("foob", "MZXW6YQ=")]
    [DataInlineUnit("fooba", "MZXW6YTB")]
    [DataInlineUnit("foobar", "MZXW6YTBOI======")]
    public void Encode_And_Decode_Should_Match_Rfc4648_Test_Vectors(string plainText, string expected)
    {
        var bytes = Encoding.ASCII.GetBytes(plainText);

        var encoded = Base32Encoding.Encode(bytes);

        encoded.Should().Be(expected);
        // Unpadded and lowercase forms extend the official vectors with representations accepted by this API.
        Base32Encoding.Encode(bytes, includePadding: false).Should().Be(expected.TrimEnd('='));
        Encoding.ASCII.GetString(Base32Encoding.Decode(expected)).Should().Be(plainText);
        Encoding.ASCII.GetString(Base32Encoding.Decode(expected.TrimEnd('=').ToLowerInvariant())).Should().Be(plainText);
    }

    // These are locally constructed negative cases based on RFC 4648 sections 3.2, 3.3, 3.5, and 6,
    // rather than test vectors published in section 10.
    // Reference: https://www.rfc-editor.org/rfc/rfc4648.html
    [Theory]
    [DataInlineUnit("A")] // One Base32 symbol cannot represent a complete input octet.
    [DataInlineUnit("MY=====")] // RFC 4648 requires six padding characters after two data symbols.
    [DataInlineUnit("MY=====A")] // Data cannot appear after padding has started.
    [DataInlineUnit("MY=======")] // Seven padding characters are non-canonical for a one-octet input.
    [DataInlineUnit("MZ======")] // The final symbol has non-zero pad bits; canonical encoding of "f" is "MY======".
    public void Decode_Should_Reject_Invalid_Or_NonCanonical_Input(string input)
    {
        var decode = () => Base32Encoding.Decode(input);

        decode.Should().Throw<FormatException>();
    }
}
