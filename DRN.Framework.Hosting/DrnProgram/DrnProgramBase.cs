using DRN.Framework.Hosting.Extensions;
using DRN.Framework.SharedKernel;
using DRN.Framework.SharedKernel.Conventions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Hosting.DrnProgram;

/// <summary>
/// <li><a href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host">Generic host model</a></li>
/// <li><a href="https://learn.microsoft.com/en-us/aspnet/core/migration/50-to-60">WebApplication - new hosting model</a></li>
/// <li><a href="https://andrewlock.net/exploring-dotnet-6-part-2-comparing-webapplicationbuilder-to-the-generic-host">Comparing WebApplication to the generic host</a></li>
/// <li><a href="https://andrewlock.net/exploring-dotnet-6-part-3-exploring-the-code-behind-webapplicationbuilder">Code behind WebApplicationBuilder</a></li>
/// <li><a href="https://andrewlock.net/exploring-the-dotnet-8-preview-comparing-createbuilder-to-the-new-createslimbuilder-method">Comparing default builder to slim builder</a></li>
/// <li><a href="https://andrewlock.net/running-async-tasks-on-app-startup-in-asp-net-core-part-1">Running async tasks at startup</a></li>
/// <li><a href="https://stackoverflow.com/questions/57846127/what-are-the-differences-between-app-userouting-and-app-useendpoints">UseRouting vs UseEndpoints</a></li>
/// </summary>
public abstract class DrnProgramBase<TProgram> where TProgram : DrnProgramBase<TProgram>, IDrnProgram, new()
{
    protected virtual DrnAppBuilderType AppBuilderType => DrnAppBuilderType.DrnDefaults;

    protected static async Task RunAsync(string[]? args = null)
    {
        try
        {
            var application = CreateApplication(args);
            await application.RunAsync();
        }
        catch (Exception exception)
        {
            Console.WriteLine("Exception Type:");
            Console.WriteLine(exception.GetType().FullName);
            Console.WriteLine("Exception Message:");
            Console.WriteLine(exception.Message);
            Console.WriteLine("Exception StackTrace:");
            Console.WriteLine(exception.StackTrace);
        }
        finally
        {
            //log shutdown
        }
    }

    public static WebApplication CreateApplication(string[]? args)
    {
        _ = JsonConventions.DefaultOptions;
        var options = new WebApplicationOptions
        {
            Args = args,
            ApplicationName = AppConstants.ApplicationName
        };

        var program = new TProgram();
        var applicationBuilder = DrnProgramConventions.GetApplicationBuilder<TProgram>(options, program.AppBuilderType);
        program.ConfigureApplicationBuilder(applicationBuilder);
        program.AddServices(applicationBuilder.Services);

        var application = applicationBuilder.Build();
        program.ConfigureApplication(application);

        return application;
    }

    protected virtual void ConfigureApplicationBuilder(WebApplicationBuilder applicationBuilder)
    {
        applicationBuilder.Configuration.AddMountDirectorySettings();
        if (AppBuilderType == DrnAppBuilderType.DrnDefaults)
            DrnProgramConventions.ConfigureDrnApplicationBuilder<TProgram>(applicationBuilder);
    }

    protected virtual void ConfigureApplication(WebApplication application)
    {
        if (AppBuilderType == DrnAppBuilderType.DrnDefaults)
            DrnProgramConventions.ConfigureDrnApplication(application);
    }

    protected abstract void AddServices(IServiceCollection services);
}