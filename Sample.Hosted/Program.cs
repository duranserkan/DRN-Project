using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Testing.Extensions;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Identity;
using Sample.Application;
using Sample.Infra;
using Sample.Infra.Identity;

namespace Sample.Hosted;

public class Program : DrnProgramBase<Program>, IDrnProgram
{
    public static async Task Main(string[] args) => await RunAsync(args);

    protected override async Task AddServicesAsync(WebApplicationBuilder builder)
    {
        builder.Services
            .AddSampleInfraServices()
            .AddSampleApplicationServices()
            .AddSampleServices(AppSettings)
            .AddIdentityApiEndpoints<IdentityUser>()
            .AddEntityFrameworkStores<SampleIdentityContext>();
        //https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity

        await builder.LaunchExternalDependenciesAsync(ScopedLog, AppSettings);
    }

    protected override void MapApplicationEndpoints(WebApplication application)
    {
        base.MapApplicationEndpoints(application);
        application.MapGroup("/identity").MapIdentityApi<IdentityUser>();
    }
}