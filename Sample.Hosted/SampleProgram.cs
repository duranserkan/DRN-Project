using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Hosting.Middlewares;
using DRN.Framework.Testing.Extensions;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Identity;
using Sample.Application;
using Sample.Hosted.Pages;
using Sample.Infra;

namespace Sample.Hosted;

public class SampleProgram : DrnProgramBase<SampleProgram>, IDrnProgram
{
    public static async Task Main(string[] args) => await RunAsync(args);

    protected override async Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog)
    {
        builder.Services
            .AddSampleInfraServices()
            .AddSampleApplicationServices()
            .AddSampleHostedServices(appSettings);

        await builder.LaunchExternalDependenciesAsync(scopedLog, appSettings);
    }

    protected override void ConfigureApplicationPreScopeStart(WebApplication application, IAppSettings appSettings)
    {
        base.ConfigureApplicationPreScopeStart(application, appSettings);
        application.UseStaticFiles();
    }

    protected override MfaRedirectionConfig ConfigureMFARedirection()
        => new(PageFor.UserManagement.EnableAuthenticator, PageFor.User.LoginWith2Fa,
            PageFor.User.Login, PageFor.User.Logout, PageFor.GetAllPages());

    protected override MfaExemptionConfig ConfigureMFAExemption()
        => new() { ExemptAuthSchemes = [IdentityConstants.BearerScheme] };
}