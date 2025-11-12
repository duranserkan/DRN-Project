using DRN.Framework.Testing.Contexts.Postgres;
using DRN.Framework.Testing.Contexts.Startup;
using DRN.Nexus.Hosted;
using DRN.Nexus.Hosted.Helpers;
using DRN.Test.Tests.Sample.Controller.Helpers;
using Sample.Hosted;
using Sample.Hosted.Helpers;

namespace DRN.Test;

public class TestStartupJob : ITestStartupJob
{
    public const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    public async Task RunAsync(StartupContext context)
    {
        PostgresContainerSettings.DefaultPassword = "drn";
        PostgresContext.PostgresContainerSettings = new();

        var dataResult = context.GetData("StartUpData.txt");
        dataResult.Data.Should().Be("Peace at Home, Peace in the World");

        await SetSampleTestUser(context);
        await SetNexusTestUser(context);
    }

    private async Task SetSampleTestUser(StartupContext context)
    {
        var methodInfo = typeof(TestStartupJob).GetMethod(nameof(SetSampleTestUser), PrivateInstance)!;
        using var DrnTestContext = context.CreateNewContext(methodInfo);
        var sampleClient = await DrnTestContext.ApplicationContext.CreateClientAsync<SampleProgram>();

        var identity = Sample.Hosted.Helpers.Get.Endpoint.User.Identity;
        var endpoints = new AuthenticationEndpoints(identity.LoginController.Login.RoutePattern!, identity.RegisterController.Register.RoutePattern!);
        AuthenticationHelper<SampleProgram>.AuthEndpoints = endpoints;

        await AuthenticationHelper<SampleProgram>.AuthenticateClientAsync(sampleClient);
    }

    private async Task SetNexusTestUser(StartupContext context)
    {
        var methodInfo = typeof(TestStartupJob).GetMethod(nameof(SetNexusTestUser), PrivateInstance)!;
        using var DrnTestContext = context.CreateNewContext(methodInfo);
        var nexusClient = await DrnTestContext.ApplicationContext.CreateClientAsync<NexusProgram>();

        var identity = Nexus.Hosted.Helpers.Get.Endpoint.User.Identity;
        var endpoints = new AuthenticationEndpoints(identity.LoginController.Login.RoutePattern!, identity.RegisterController.Register.RoutePattern!);
        AuthenticationHelper<NexusProgram>.AuthEndpoints = endpoints;

        await AuthenticationHelper<NexusProgram>.AuthenticateClientAsync(nexusClient);
    }
}