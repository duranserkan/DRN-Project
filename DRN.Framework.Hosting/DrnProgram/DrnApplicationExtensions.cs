using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Hosting.DrnProgram;

public static class AppBuilderExtensions
{
    //Todo implement application dependency summary as well(Packages, projects etc)
    
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_ApplicationBuilder")]
    private static extern ApplicationBuilder GetApplicationBuilder(WebApplication app);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_components")]
    private static extern ref List<Func<RequestDelegate, RequestDelegate>> GetComponents(ApplicationBuilder builder);

    public static RequestPipelineSummary GetRequestPipelineSummary(this WebApplication app)
    {
        IList<string> startupFilters = [];
        IList<string> middlewares; //todo get middleware details such as in which stage added in the ConfigureApplication pipeline
        try
        {
            var applicationBuilder = GetApplicationBuilder(app);

            var filters = app.Services.GetServices<IStartupFilter>();
            startupFilters = filters.Select(f => f.GetType().FullName ?? string.Empty).ToList();

            var components = GetComponents(applicationBuilder) ?? [];
            
            middlewares = components.Select(x => x.Target?.ToString() ?? string.Empty).ToList();
        }
        catch (Exception e)
        {
            middlewares = [e.GetType().FullName ?? string.Empty, e.Message, e.StackTrace ?? string.Empty];
        }

        return new RequestPipelineSummary(startupFilters, middlewares);
    }
}

public record RequestPipelineSummary(IList<string> StartupFilters, IList<string> Middlewares);