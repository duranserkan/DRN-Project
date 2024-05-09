using DRN.Framework.Hosting.DrnProgram;
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
            .AddSampleApplicationServices();
    }

    protected override void ConfigureApplicationPreScopeStart(WebApplication application)
    {
        base.ConfigureApplicationPreScopeStart(application);
        if (!AppSettings.IsDevEnvironment) return;

        DrnProgramOptions.UseHttpRequestLogger = true;
    }

    protected override void ConfigureApplicationPreAuth(WebApplication application)
    {
        base.ConfigureApplicationPreAuth(application);
        if (!AppSettings.IsDevEnvironment) return;

        application.MapSwagger();
        application.UseSwaggerUI();
    }
}