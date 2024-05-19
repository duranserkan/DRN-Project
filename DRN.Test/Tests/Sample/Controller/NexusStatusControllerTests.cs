using Flurl.Http;
using Flurl.Http.Testing;
using Sample.Hosted;
using Xunit.Abstractions;

namespace DRN.Test.Tests.Sample.Controller;

public class NexusStatusControllerTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [DataInline]
    public async Task StatusController_Should_Return_Status(TestContext context)
    {
        using var httpTest = new HttpTest();
        httpTest.RespondWith("all conditions met!");

        context.ApplicationContext.LogToTestOutput(outputHelper);
        var application = context.ApplicationContext.CreateApplication<Program>();
        await context.ContainerContext.Postgres.ApplyMigrationsAsync();
        application.Server.PreserveExecutionContext = true;

        var client = application.CreateClient();
        var response = await client.GetAsync("NexusStatus");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNull();
    }
}