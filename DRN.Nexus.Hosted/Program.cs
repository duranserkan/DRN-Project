using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Testing.Extensions;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using DRN.Nexus.Application;
using DRN.Nexus.Infra;
using Microsoft.AspNetCore.Identity;

namespace DRN.Nexus.Hosted;

public class NexusProgram : DrnProgramBase<NexusProgram>, IDrnProgram
{
    public static async Task Main(string[] args) => await RunAsync(args);

    protected override async Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog)
    {
        builder.Services
            .AddNexusInfraServices()
            .AddNexusApplicationServices()
            .AddNexusHostedServices(appSettings);

        await builder.LaunchExternalDependenciesAsync(scopedLog, appSettings);
    }

    protected override MfaExemptionConfig ConfigureMFAExemption()
        => new() { ExemptAuthSchemes = [IdentityConstants.BearerScheme] };
    
    // protected override MfaRedirectionConfig ConfigureMFARedirection()
    //     => new(PageFor.UserManagement.EnableAuthenticator, PageFor.User.LoginWith2Fa,
    //         PageFor.User.Login, PageFor.User.Logout, PageFor.GetAllPages());
    
    protected override void ConfigureApplicationPreScopeStart(WebApplication application, IAppSettings appSettings)
    {
        base.ConfigureApplicationPreScopeStart(application, appSettings);
        application.UseStaticFiles();
    }
}