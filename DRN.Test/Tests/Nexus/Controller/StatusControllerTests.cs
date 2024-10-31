using System.Net.Http.Json;
using DRN.Framework.Utils.Configurations;
using DRN.Nexus.Hosted;
using DRN.Nexus.Hosted.Controllers;
using Xunit.Abstractions;

namespace DRN.Test.Tests.Nexus.Controller;

public class StatusControllerTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [DataInline]
    public async Task StatusController_Should_Return_Status(TestContext context)
    {
        var application = await context.ApplicationContext.CreateApplicationAndBindDependenciesAsync<Program>(outputHelper);

        var client = application.CreateClient();
        var status = await client.GetFromJsonAsync<ConfigurationDebugViewSummary>(EndpointFor.Status.Status.RoutePattern);
        var programName = typeof(Program).GetAssemblyName();

        status?.ApplicationName.Should().Be(programName);
    }
}