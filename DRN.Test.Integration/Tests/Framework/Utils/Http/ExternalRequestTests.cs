using System.Net;
using DRN.Framework.Utils.Http;
using Flurl.Http;

namespace DRN.Test.Integration.Tests.Framework.Utils.Http;

public class ExternalRequestTests
{
    [Theory]
    [DataInline("What can be asserted without evidence can be dismissed without evidence")]
    public async Task ExternalRequest_Should_Return_Response(DrnTestContext context, string responseText)
    {
        var endpoint = "https://hitchensrazor.com";
        context.FlurlHttpTest.ForCallsTo(endpoint).RespondWith(responseText, 201);

        var externalRequest = context.GetRequiredService<IExternalRequest>();
        var request = externalRequest.For(endpoint, HttpVersion.Version20);
        var response = await request.GetAsync().ToStringAsync();

        response.HttpStatus.Should().Be(201);
        response.Payload.Should().Be(responseText);
    }

    [Theory]
    [DataInline]
    public async Task ExternalRequest_Should_Return_Client_Error_From_Try_Converter(DrnTestContext context)
    {
        var endpoint = "https://example.test/validation";
        var responseText = "Validation failed.";
        context.FlurlHttpTest.ForCallsTo(endpoint).RespondWith(responseText, 422);

        var externalRequest = context.GetRequiredService<IExternalRequest>();
        var result = await externalRequest.For(endpoint, HttpVersion.Version20).GetAsync().TryToStringAsync();

        result.ResponseReceived.Should().BeTrue();
        result.HttpStatus.Should().Be(422);
        result.StatusClass.Should().Be(HttpStatusClass.ClientError);
        result.IsSuccessStatusCode.Should().BeFalse();
        result.IsSuccess.Should().BeFalse();
        result.Payload.Should().Be(responseText);
        result.Failure.Should().BeNull();
    }
}
