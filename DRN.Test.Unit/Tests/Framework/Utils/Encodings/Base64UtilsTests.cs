using DRN.Framework.Utils.Encodings;
using DRN.Framework.Utils.Models.Sample;
using AwesomeAssertions;
using Xunit;

namespace DRN.Test.Unit.Tests.Framework.Utils.Encodings;

public class Base64UtilsTests
{
    [Fact]
    public void String_Should_Be_Base64Encoded_And_Decoded()
    {
        var forecasts = WeatherForecast.Get();
        var encodedJson = Base64Utils.UrlSafeBase64Encode(forecasts);
        var decodedModel = Base64Utils.UrlSafeBase64Decode<WeatherForecast[]>(encodedJson);
        
        forecasts.Should().BeEquivalentTo(decodedModel);
    }
}