using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Testing.Extensions;
using DRN.Framework.Utils.Settings;
using Sample.Application;
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

    protected override void ConfigureSwaggerOptions(DrnProgramSwaggerOptions options, IAppSettings appSettings)
    {
        base.ConfigureSwaggerOptions(options, appSettings);
        options.AddBearerTokenSecurityRequirement = false;
    }
}