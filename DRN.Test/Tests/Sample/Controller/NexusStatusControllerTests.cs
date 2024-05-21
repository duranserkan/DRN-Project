using DRN.Framework.Utils.Http;
using Flurl.Http;
using Sample.Hosted;
using Xunit.Abstractions;

namespace DRN.Test.Tests.Sample.Controller;

public class NexusStatusControllerTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [DataInline]
    public async Task StatusController_Should_Return_Status(TestContext context, string mockPayload)
    {
        context.FlurlHttpTest.RespondWith(mockPayload);

        var client = await context.ApplicationContext.CreateClientFor<Program>(outputHelper);
        var response = await client.Request("NexusStatus").GetAsync().ToStringAsync();

        response.Payload.Should().Be(mockPayload);
    }
}