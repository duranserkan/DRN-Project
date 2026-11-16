using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Mvc;

namespace Consumer
{
    public static class ConsumerModule
    {
        public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddConsumerServices(
            this Microsoft.Extensions.DependencyInjection.IServiceCollection services)
        {
            DRN.Framework.Utils.DependencyInjection.ServiceCollectionExtensions.AddServicesWithAttributes(services);
            return services;
        }
    }

    public class AppProgram : DrnProgramBase<AppProgram>, IDrnProgram
    {
        public static Task Main(string[] args) => Task.CompletedTask;

        protected override EndpointCollection<AppProgram> ConfigureEndpointCollection()
            => Helpers.EndpointFor.AppEndpointFor.EndpointCollection;

        protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings settings, IScopedLog log)
        {
            builder.Services.AddConsumerServices();
            // Compilation must consume generated APIs, not merely load an analyzer asset.
            ApiEndpoint endpoint = new Helpers.EndpointFor.AppEndpointFor().Items.Read;
            string page = new Helpers.PageFor.AppPageFor().Root.Index;
            _ = endpoint;
            _ = page;
            return Task.CompletedTask;
        }
    }
}

namespace Consumer.Controllers
{
    [Route("/items")]
    public class ItemsController : ControllerBase
    {
        [HttpGet]
        public string Read() => "items";
    }
}
