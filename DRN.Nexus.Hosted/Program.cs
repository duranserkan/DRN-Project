using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Testing.Extensions;
using DRN.Nexus.Application;
using DRN.Nexus.Infra;
using DRN.Nexus.Infra.Identity;
using Microsoft.AspNetCore.Identity;

namespace DRN.Nexus.Hosted;

public class Program : DrnProgramBase<Program>, IDrnProgram
{
    public static async Task Main(string[] args) => await RunAsync(args);

    protected override async Task AddServicesAsync(WebApplicationBuilder builder)
    {
        builder.Services
            .AddNexusInfraServices()
            .AddNexusApplicationServices()
            .AddNexusServices(AppSettings)
            .AddIdentityApiEndpoints<IdentityUser>()
            .AddEntityFrameworkStores<NexusIdentityContext>();
        if (!AppSettings.IsDevEnvironment) return;

        var launchResult = await builder.LaunchExternalDependenciesAsync();
        ScopedLog.Add(nameof(launchResult.PostgresConnection), launchResult.PostgresConnection);
    }

    protected override void MapApplicationEndpoints(WebApplication application)
    {
        base.MapApplicationEndpoints(application);
        application.MapGroup("/account").MapIdentityApi<IdentityUser>();
    }
}