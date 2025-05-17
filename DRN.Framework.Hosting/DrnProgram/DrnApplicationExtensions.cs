using DRN.Framework.Utils.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Hosting.DrnProgram;

public static class AppBuilderExtensions
{
    //Todo implement application dependency summary as well(Packages, projects etc)
    
    public static RequestPipelineSummary GetRequestPipelineSummary(this WebApplication app)
    {
        IList<string> startupFilters = [];
        IList<string> middlewares; //todo get middleware details such as in which stage added in the ConfigureApplication pipeline
        try
        {
            var applicationBuilder = (ApplicationBuilder)typeof(WebApplication)
                .GetProperty(nameof(ApplicationBuilder), BindingFlag.InstanceNonPublic)
                !.GetValue(app)!;

            var filters = app.Services.GetServices<IStartupFilter>();
            startupFilters = filters.Select(f => f.GetType().FullName ?? string.Empty).ToList();

            var components = (List<Func<RequestDelegate, RequestDelegate>>?)typeof(ApplicationBuilder)
                .GetField("_components",BindingFlag.InstanceNonPublic)?
                .GetValue(applicationBuilder)?? [];
            
            middlewares = components.Select(x=>x.Target?.ToString() ?? string.Empty).ToList();
        }
        catch (Exception e)
        {
            middlewares = [e.GetType().FullName ?? string.Empty, e.Message, e.StackTrace ?? string.Empty];
        }

        return new RequestPipelineSummary(startupFilters, middlewares);
    }
}

public record RequestPipelineSummary(IList<string> StartupFilters, IList<string> Middlewares);