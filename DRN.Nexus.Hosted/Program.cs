using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using DRN.Nexus.Application;

namespace DRN.Nexus.Hosted;

public class NexusProgram : DrnProgramBase<NexusProgram>, IDrnProgram
{
    public static async Task Main(string[] args) => await RunAsync(args);

    protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog)
        => Task.FromResult(builder.Services.AddNexusHostedServices(appSettings));

#pragma warning disable S125 // Keep this opt-in example to show how a host maps MFA redirects to its Identity pages.
    // protected override MfaRedirectionConfig ConfigureMFARedirection()
    //     => new(PageFor.UserManagement.EnableAuthenticator, PageFor.User.LoginWith2Fa,
    //         PageFor.User.Login, PageFor.User.Logout, PageFor.GetAllPages());
#pragma warning restore S125

    protected override void ConfigureApplicationPreScopeStart(WebApplication application, IAppSettings appSettings)
    {
        base.ConfigureApplicationPreScopeStart(application, appSettings);
        application.UseStaticFiles();
    }
}
