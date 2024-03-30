using DRN.Framework.Hosting.DrnProgram;
using DRN.Nexus.Application;
using DRN.Nexus.Infra;

namespace DRN.Nexus.Hosted;

public class Program : DrnProgramBase<Program>, IDrnProgram
{
    public static async Task Main(string[] args)
    {
        try
        {
            await RunAsync(args);
        }
        catch (Exception e)
        {
            _ = e; //you can use this line to add break point for debugging.
            throw;
        }
    }

    protected override void AddServices(IServiceCollection services) => services
            .AddNexusInfraServices()
            .AddNexusApplicationServices();
}