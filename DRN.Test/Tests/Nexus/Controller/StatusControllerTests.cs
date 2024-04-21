using System.Net.Http.Json;
using DRN.Framework.Utils.Configurations;
using DRN.Nexus.Hosted;
using Xunit.Abstractions;

namespace DRN.Test.Tests.Nexus.Controller;

public class StatusControllerTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [DataInline]
    public async Task StatusController_Should_Return_Status(TestContext context)
    {
        context.ApplicationContext.LogToTestOutput(outputHelper);
        var application = context.ApplicationContext.CreateApplication<Program>();
        //await context.ContainerContext.Postgres.ApplyMigrationsAsync();

        var client = application.CreateClient();
        var status = await client.GetFromJsonAsync<ConfigurationDebugViewSummary>("Status");
        var programName = typeof(Program).GetAssemblyName();
        status?.ApplicationName.Should().Be(programName);
    }
}