using DRN.Framework.Hosting.DrnProgram;
using DRN.Nexus.Application;
using DRN.Nexus.Infra;

namespace DRN.Nexus.Hosted;

public class Program : DrnProgramBase<Program>, IDrnProgram
{
    public static async Task Main(string[] args) => await RunAsync(args);

    protected override void AddServices(IServiceCollection services) => services
        .AddNexusInfraServices()
        .AddNexusApplicationServices();
}