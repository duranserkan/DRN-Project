using System.Reflection;
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Utils.DependencyInjection;
using DRN.Framework.Utils.Http;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using Flurl.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace DRN.Test.Utils.Hosting;

public sealed class ChainedNodeAProgram : DrnProgramBase<ChainedNodeAProgram>, IDrnProgram
{
    public const string NodeName = "node-a";

    public static async Task Main(string[] args) => await RunAsync(args);

    protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog)
    {
        builder.Services.AddServicesWithAttributes();
        return Task.CompletedTask;
    }

    protected override void ConfigureMvcBuilder(IMvcBuilder mvcBuilder, IAppSettings appSettings)
    {
        base.ConfigureMvcBuilder(mvcBuilder, appSettings);
        mvcBuilder.ScopeToController<ChainedNodeAController>();
    }
}

public sealed class ChainedNodeBProgram : DrnProgramBase<ChainedNodeBProgram>, IDrnProgram
{
    public const string NodeName = "node-b";

    public static async Task Main(string[] args) => await RunAsync(args);

    protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog)
    {
        builder.Services.AddServicesWithAttributes();
        return Task.CompletedTask;
    }

    protected override void ConfigureMvcBuilder(IMvcBuilder mvcBuilder, IAppSettings appSettings)
    {
        base.ConfigureMvcBuilder(mvcBuilder, appSettings);
        mvcBuilder.ScopeToController<ChainedNodeBController>();
    }
}

public sealed class ChainedNodeCProgram : DrnProgramBase<ChainedNodeCProgram>, IDrnProgram
{
    public const string NodeName = "node-c";

    public static async Task Main(string[] args) => await RunAsync(args);

    protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog)
    {
        builder.Services.AddServicesWithAttributes();
        return Task.CompletedTask;
    }

    protected override void ConfigureMvcBuilder(IMvcBuilder mvcBuilder, IAppSettings appSettings)
    {
        base.ConfigureMvcBuilder(mvcBuilder, appSettings);
        mvcBuilder.ScopeToController<ChainedNodeCController>();
    }
}

public sealed class BidirectionalNode1Program : DrnProgramBase<BidirectionalNode1Program>, IDrnProgram
{
    public const string NodeName = "node-1";

    public static async Task Main(string[] args) => await RunAsync(args);

    protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog)
    {
        builder.Services.AddServicesWithAttributes();
        return Task.CompletedTask;
    }

    protected override void ConfigureMvcBuilder(IMvcBuilder mvcBuilder, IAppSettings appSettings)
    {
        base.ConfigureMvcBuilder(mvcBuilder, appSettings);
        mvcBuilder.ScopeToController<BidirectionalNode1Controller>();
    }
}

public sealed class BidirectionalNode2Program : DrnProgramBase<BidirectionalNode2Program>, IDrnProgram
{
    public const string NodeName = "node-2";

    public static async Task Main(string[] args) => await RunAsync(args);

    protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog)
    {
        builder.Services.AddServicesWithAttributes();
        return Task.CompletedTask;
    }

    protected override void ConfigureMvcBuilder(IMvcBuilder mvcBuilder, IAppSettings appSettings)
    {
        base.ConfigureMvcBuilder(mvcBuilder, appSettings);
        mvcBuilder.ScopeToController<BidirectionalNode2Controller>();
    }
}

internal static class MultiAppMvcBuilderExtensions
{
    public static IMvcBuilder ScopeToController<TController>(this IMvcBuilder mvcBuilder) where TController : ControllerBase
    {
        mvcBuilder.ConfigureApplicationPartManager(manager =>
        {
            var existing = manager.FeatureProviders.OfType<ControllerFeatureProvider>().ToList();
            foreach (var provider in existing)
                manager.FeatureProviders.Remove(provider);

            manager.FeatureProviders.Add(new SpecificControllerFeatureProvider(typeof(TController)));
        });
        return mvcBuilder;
    }

    private sealed class SpecificControllerFeatureProvider(Type controllerType) : ControllerFeatureProvider
    {
        protected override bool IsController(TypeInfo typeInfo) => typeInfo.AsType() == controllerType;
    }
}

[ApiController]
[Route("api/chain")]
public class ChainedNodeAController(IInternalRequest internalRequest) : ControllerBase
{
    [HttpGet("start")]
    [AllowAnonymous]
    public async Task<IActionResult> StartAsync()
    {
        var response = await internalRequest.For(ChainedNodeBProgram.NodeName).AppendPathSegment("api/chain/step").GetStringAsync();
        return Ok($"A -> {response}");
    }
}

[ApiController]
[Route("api/chain")]
public class ChainedNodeBController(IInternalRequest internalRequest) : ControllerBase
{
    [HttpGet("step")]
    [AllowAnonymous]
    public async Task<IActionResult> StepAsync()
    {
        var response = await internalRequest.For(ChainedNodeCProgram.NodeName).AppendPathSegment("api/chain/leaf").GetStringAsync();
        return Ok($"B -> {response}");
    }
}

[ApiController]
[Route("api/chain")]
public class ChainedNodeCController : ControllerBase
{
    [HttpGet("leaf")]
    [AllowAnonymous]
    public IActionResult Leaf() => Ok("C");
}

[ApiController]
[Route("api/node1")]
public class BidirectionalNode1Controller(IInternalRequest internalRequest) : ControllerBase
{
    [HttpGet("ping")]
    [AllowAnonymous]
    public async Task<IActionResult> PingAsync()
    {
        var response = await internalRequest.For(BidirectionalNode2Program.NodeName).AppendPathSegment("api/node2/pong").GetStringAsync();
        return Ok($"1-ping({response})");
    }

    [HttpGet("pong")]
    [AllowAnonymous]
    public IActionResult Pong() => Ok("1-pong");
}

[ApiController]
[Route("api/node2")]
public class BidirectionalNode2Controller(IInternalRequest internalRequest) : ControllerBase
{
    [HttpGet("ping")]
    [AllowAnonymous]
    public async Task<IActionResult> PingAsync()
    {
        var response = await internalRequest.For(BidirectionalNode1Program.NodeName).AppendPathSegment("api/node1/pong").GetStringAsync();
        return Ok($"2-ping({response})");
    }

    [HttpGet("pong")]
    [AllowAnonymous]
    public IActionResult Pong() => Ok("2-pong");
}
