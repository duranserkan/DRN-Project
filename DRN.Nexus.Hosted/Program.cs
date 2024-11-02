using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Testing.Extensions;
using DRN.Nexus.Application;
using DRN.Nexus.Infra;
using Microsoft.AspNetCore.Identity;

namespace DRN.Nexus.Hosted;

public class NexusProgram : DrnProgramBase<NexusProgram>, IDrnProgram
{
    public static async Task Main(string[] args) => await RunAsync(args);

    protected override async Task AddServicesAsync(WebApplicationBuilder builder)
    {
        builder.Services
            .AddNexusInfraServices()
            .AddNexusApplicationServices()
            .AddNexusHostedServices(AppSettings);

        await builder.LaunchExternalDependenciesAsync(ScopedLog, AppSettings);
    }

    protected override MFAExemptionConfig ConfigureMFAExemption()
        => new() { ExemptAuthSchemes = [IdentityConstants.BearerScheme] };
}