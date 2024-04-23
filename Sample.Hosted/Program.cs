using DRN.Framework.Hosting.DrnProgram;
using Sample.Application;
using Sample.Infra;

namespace Sample.Hosted;

public class Program : DrnProgramBase<Program>, IDrnProgram
{
    public static async Task Main(string[] args) => await RunAsync(args);

    protected override void AddServices(IServiceCollection services) => services
        .AddSampleInfraServices()
        .AddSampleApplicationServices();
}