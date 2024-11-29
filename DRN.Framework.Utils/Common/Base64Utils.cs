using System.Buffers.Text;
using System.Text;

namespace DRN.Framework.Utils.Common;

//todo: write tests
public class Base64Utils
{
    public static string UrlSafeBase64Decode(string input)
    {
        var array = Base64Url.DecodeFromChars(input.ToCharArray());
        var decodedString = Encoding.UTF8.GetString(array);

        return decodedString;
    }

    public static string UrlSafeBase64Encode(string input)
    {
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var encodedString = Base64Url.EncodeToString(inputBytes);

        return encodedString;
    }

    public static string UrlSafeBase64Encode(byte[] input) => Base64Url.EncodeToString(input);
}