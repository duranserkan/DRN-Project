using Sample.Hosted;
using Sample.Hosted.Helpers;

namespace DRN.Test.Tests.Sample.Controller.QA;

public class TagControllerTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [DataInline]
    public async Task TagController_Should_Return_Tags(TestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>(outputHelper);
        var tagEndpoint = Get.Endpoint.QA.Tag.GetAsync.RoutePattern;
        //var tagResult = await client.GetFromJsonAsync<PaginationResultModel<Tag>>(tagEndpoint);
        //todo complete tag controller tests
    }
}