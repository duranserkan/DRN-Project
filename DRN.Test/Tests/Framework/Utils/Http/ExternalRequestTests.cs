using DRN.Framework.Utils.Http;
using Flurl.Http;

namespace DRN.Test.Tests.Framework.Utils.Http;

public class ExternalRequestTests
{
    [Theory]
    [DataInline("What can be asserted without evidence can be dismissed without evidence")]
    public async Task ExternalRequest_Should_Return_Response(TestContext context, string responseText)
    {
        var endpoint = "https://hitchensrazor.com";
        context.FlurlHttpTest.ForCallsTo(endpoint).RespondWith(responseText, 201);

        var externalRequest = context.GetRequiredService<IExternalRequest>();
        var request = externalRequest.For(endpoint, new Version("2.0"));
        var response = await request.GetAsync().ToStringAsync();

        response.HttpStatus.Should().Be(201);
        response.Payload.Should().Be(responseText);
    }
}