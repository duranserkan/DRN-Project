using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Hosting.Middlewares;
using DRN.Framework.Testing.Extensions;
using Sample.Application;
using Sample.Hosted.Pages;
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
            .AddSampleHostedServices(AppSettings);

        await builder.LaunchExternalDependenciesAsync(ScopedLog, AppSettings);
    }

    protected override void ConfigureApplicationPreScopeStart(WebApplication application)
    {
        base.ConfigureApplicationPreScopeStart(application);
        application.UseStaticFiles();
    }

    protected override MFARedirectionConfig ConfigureMFARedirection()
        => new(PageFor.UserManagement.EnableAuthenticator, PageFor.User.LoginWith2Fa,
            PageFor.User.Login, PageFor.User.Logout, PageFor.GetAllPages());
}