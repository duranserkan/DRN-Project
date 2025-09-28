using System.Diagnostics.CodeAnalysis;
using DRN.Framework.Utils.Data.Encodings;
using DRN.Framework.Utils.Models.Sample;

namespace DRN.Test.Unit.Tests.Framework.Utils.Encodings;

//todo write serialization tests
public class EncodingExtensionTests
{
    [Fact]
    [SuppressMessage("ReSharper", "RedundantArgumentDefaultValue")]
    public void Model_Should_Be_Serialized_ToJson_And_Encoded_And_Decoded()
    {
        var forecasts = WeatherForecast.Get();
        var encodedJson = forecasts.Encode(ByteEncoding.Base64UrlEncoded);
        var decodedModel = encodedJson.Decode<WeatherForecast[]>(ByteEncoding.Base64UrlEncoded);
        forecasts.Should().BeEquivalentTo(decodedModel);
        
        var encodedJsonWithDefaultEncoding = forecasts.Encode();
        encodedJson.Should().Be(encodedJsonWithDefaultEncoding);
        
        var decodedModelWithDefaultEncoding = encodedJsonWithDefaultEncoding.Decode<WeatherForecast[]>();
        decodedModel.Should().BeEquivalentTo(decodedModelWithDefaultEncoding);

        encodedJson = forecasts.Encode(ByteEncoding.Base64);
        decodedModel = encodedJson.Decode<WeatherForecast[]>(ByteEncoding.Base64);
        forecasts.Should().BeEquivalentTo(decodedModel);
        
        encodedJson = forecasts.Encode(ByteEncoding.Hex);
        decodedModel = encodedJson.Decode<WeatherForecast[]>(ByteEncoding.Hex);
        forecasts.Should().BeEquivalentTo(decodedModel);
        
        encodedJson = forecasts.Encode(ByteEncoding.Utf8);
        decodedModel = encodedJson.Decode<WeatherForecast[]>(ByteEncoding.Utf8);
        forecasts.Should().BeEquivalentTo(decodedModel);
    }
}