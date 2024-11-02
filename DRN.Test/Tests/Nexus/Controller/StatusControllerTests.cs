using System.Net.Http.Json;
using DRN.Framework.Utils.Configurations;
using DRN.Nexus.Hosted;
using DRN.Nexus.Hosted.Controllers;
using DRN.Test.Tests.Sample.Controller.Helpers;
using Xunit.Abstractions;

namespace DRN.Test.Tests.Nexus.Controller;

public class StatusControllerTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [DataInline]
    public async Task StatusController_Should_Return_Status(TestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<NexusProgram>(outputHelper);
        var user = await AuthenticationHelper<NexusProgram>.AuthenticateClientAsync(client);
        var status = await client.GetFromJsonAsync<ConfigurationDebugViewSummary>(NexusEndpointFor.Status.Status.RoutePattern);
        var programName = typeof(NexusProgram).GetAssemblyName();

        status?.ApplicationName.Should().Be(programName);
    }
}