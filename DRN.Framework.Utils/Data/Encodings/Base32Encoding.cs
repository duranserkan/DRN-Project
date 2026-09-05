using System.Security.Cryptography;

namespace DRN.Framework.Utils.Data.Encodings;

/// <summary>
/// Encodes and decodes RFC 4648 Base32 data using the standard alphabet.
/// </summary>
public static class Base32Encoding
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>
    /// Encodes bytes as canonical padded Base32, or as unpadded Base32 when requested.
    /// </summary>
    public static string Encode(ReadOnlySpan<byte> bytes, bool includePadding = true)
    {
        if (bytes.IsEmpty)
            return string.Empty;

        var unpaddedLength = checked((int)(((long)bytes.Length * 8 + 4) / 5));
        var outputLength = includePadding
            ? checked((int)(((long)unpaddedLength + 7) / 8 * 8))
            : unpaddedLength;
        var encoded = new char[outputLength];
        var buffer = 0;
        var bitsInBuffer = 0;
        var outputIndex = 0;

        foreach (var value in bytes)
        {
            buffer = (buffer << 8) | value;
            bitsInBuffer += 8;

            while (bitsInBuffer >= 5)
            {
                bitsInBuffer -= 5;
                encoded[outputIndex++] = Alphabet[(buffer >> bitsInBuffer) & 0x1f];
                buffer &= (1 << bitsInBuffer) - 1;
            }
        }

        if (bitsInBuffer > 0)
            encoded[outputIndex++] = Alphabet[(buffer << (5 - bitsInBuffer)) & 0x1f];

        while (outputIndex < encoded.Length)
            encoded[outputIndex++] = '=';

        return new string(encoded);
    }

    /// <summary>
    /// Decodes canonical padded or unpadded Base32 text.
    /// </summary>
    public static byte[] Decode(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Length == 0)
            return [];

        var paddingIndex = input.IndexOf('=');
        var dataLength = paddingIndex < 0 ? input.Length : paddingIndex;
        var remainder = dataLength % 8;
        if (remainder is 1 or 3 or 6)
            throw new FormatException("The Base32 input has an invalid encoded length.");

        ValidatePadding(input, dataLength, remainder);

        var decodedLength = checked((int)((long)dataLength * 5 / 8));
        var decoded = new byte[decodedLength];
        var buffer = 0;
        var bitsInBuffer = 0;
        var outputIndex = 0;

        try
        {
            for (var index = 0; index < dataLength; index++)
            {
                buffer = (buffer << 5) | DecodeCharacter(input[index]);
                bitsInBuffer += 5;

                if (bitsInBuffer < 8)
                    continue;

                bitsInBuffer -= 8;
                decoded[outputIndex++] = (byte)(buffer >> bitsInBuffer);
                buffer &= (1 << bitsInBuffer) - 1;
            }

            if (bitsInBuffer > 0 && buffer != 0)
                throw new FormatException("The Base32 input contains non-zero trailing bits.");

            return decoded;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(decoded);
            throw;
        }
    }

    private static int DecodeCharacter(char character)
    {
        if (character is >= 'a' and <= 'z')
            character = (char)(character - ('a' - 'A'));

        return character switch
        {
            >= 'A' and <= 'Z' => character - 'A',
            >= '2' and <= '7' => character - '2' + 26,
            _ => throw new FormatException($"The Base32 input contains invalid character '{character}'.")
        };
    }

    private static void ValidatePadding(string input, int dataLength, int remainder)
    {
        if (dataLength == input.Length)
            return;

        for (var index = dataLength; index < input.Length; index++)
            if (input[index] != '=')
                throw new FormatException("Base32 padding must appear only at the end of the input.");

        if (input.Length % 8 != 0)
            throw new FormatException("Padded Base32 input must contain a multiple of eight characters.");

        var expectedPadding = (8 - remainder) % 8;
        if (input.Length - dataLength != expectedPadding)
            throw new FormatException("The Base32 input contains non-canonical padding.");
    }
}
