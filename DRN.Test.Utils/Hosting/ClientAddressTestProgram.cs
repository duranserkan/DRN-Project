using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;

namespace DRN.Test.Utils.Hosting;

public sealed class ClientAddressTestProgram : DrnProgramBase<ClientAddressTestProgram>, IDrnProgram
{
    public static async Task Main(string[] args) => await RunAsync(args);

    protected override void ConfigureApplication(WebApplication application, IAppSettings appSettings)
    {
        application.MapGet("/resource", GetResource);
        application.MapGet("/api/resource", GetResource);
    }

    private static ClientAddressResource GetResource(HttpRequest request) => new("test-resource", request.IsHttps);

    protected override void ValidateEndpoints(WebApplication application, IAppSettings appSettings)
    {
        // These minimal endpoints only verify client addresses and do not use framework endpoint discovery.
    }

    protected override Task ValidateServicesAsync(WebApplication application, IScopedLog scopeLog) => Task.CompletedTask;

    protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog) => Task.CompletedTask;
}

public sealed record ClientAddressResource(string Content, bool IsHttps);
