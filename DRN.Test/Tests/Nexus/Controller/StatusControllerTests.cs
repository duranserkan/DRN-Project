using System.Net.Http.Json;
using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.Utils.Configurations;
using DRN.Nexus.Hosted;

namespace DRN.Test.Tests.Nexus.Controller;

public class StatusControllerTests
{
    [Theory]
    [DataInline]
    public async Task StatusController_Should_Return_Status(TestContext context)
    {
        var webApplication = context.WebApplicationContext.CreateWebApplication<Program>();
        //await context.ContainerContext.StartPostgresAndApplyMigrationsAsync();

        var client = webApplication.CreateClient();
        var status = await client.GetFromJsonAsync<ConfigurationDebugViewSummary>("Status");
        status?.AppEnvironment.Should().Be(AppEnvironment.Staging);
    }
}