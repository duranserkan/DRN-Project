using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Testing.Extensions;
using Microsoft.AspNetCore.Identity;
using Sample.Application;
using Sample.Hosted.Auth.EndpointRouteBuilderExtensions.Endpoints;
using Sample.Infra;

namespace Sample.Hosted;

public class Program : DrnProgramBase<Program>, IDrnProgram
{
    public static async Task Main(string[] args) => await RunAsync(args);

    protected override async Task AddServicesAsync(WebApplicationBuilder builder)
    {
        builder.Services
            .AddSampleInfraServices()
            .AddSampleApplicationServices()
            .AddSampleServices(AppSettings);

        await builder.LaunchExternalDependenciesAsync(ScopedLog, AppSettings);
    }

    protected override void ConfigureApplicationPreScopeStart(WebApplication application)
    {
        base.ConfigureApplicationPreScopeStart(application);
        application.UseStaticFiles();
    }
//todo: redirection middleware for pending mfa
//todo: swagger access issue
    protected override void MapApplicationEndpoints(WebApplication application)
    {
        base.MapApplicationEndpoints(application);
        application.MapRazorPages();
        application.MapDrnIdentityApi<IdentityUser>("identity");
    }
}