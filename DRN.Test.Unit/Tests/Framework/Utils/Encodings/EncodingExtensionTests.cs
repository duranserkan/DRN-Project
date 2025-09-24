using DRN.Framework.Utils.Encodings;
using DRN.Framework.Utils.Models.Sample;

namespace DRN.Test.Unit.Tests.Framework.Utils.Encodings;

//todo add encoding and hashing test cases, fix failing test cases
public class EncodingExtensionTests
{
    [Fact]
    public void String_Should_Be_Base64Encoded_And_Decoded()
    {
        var forecasts = WeatherForecast.Get();
        var encodedJson = forecasts.Encode(ByteEncoding.Base64UrlEncoded);
        var decodedModel = encodedJson.Decode<WeatherForecast[]>(ByteEncoding.Base64UrlEncoded);
        forecasts.Should().BeEquivalentTo(decodedModel);

        // encodedJson = forecasts.Encode(ByteEncoding.Base64);
        // decodedModel = encodedJson.Decode<WeatherForecast[]>(ByteEncoding.Base64);
        // forecasts.Should().BeEquivalentTo(decodedModel);
        //
        // encodedJson = forecasts.Encode(ByteEncoding.Hex);
        // decodedModel = encodedJson.Decode<WeatherForecast[]>(ByteEncoding.Hex);
        // forecasts.Should().BeEquivalentTo(decodedModel);
    }
}