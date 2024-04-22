using DRN.Framework.Hosting.Middlewares;
using DRN.Framework.SharedKernel.Conventions;
using DRN.Framework.Utils.DependencyInjection;
using DRN.Framework.Utils.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;

namespace DRN.Framework.Hosting.DrnProgram;

public static class DrnProgramConventions
{
    public static WebApplicationBuilder GetApplicationBuilder<TProgram>(WebApplicationOptions options, DrnAppBuilderType drnAppBuilderType)
        where TProgram : DrnProgramBase<TProgram>, IDrnProgram, new()
    {
        var builder = drnAppBuilderType switch
        {
            DrnAppBuilderType.DrnDefaults => WebApplication.CreateEmptyBuilder(options),
            DrnAppBuilderType.Empty => WebApplication.CreateEmptyBuilder(options),
            DrnAppBuilderType.Slim => WebApplication.CreateSlimBuilder(options),
            DrnAppBuilderType.Default => WebApplication.CreateBuilder(options),
            _ => throw new ArgumentOutOfRangeException()
        };

        return builder;
    }

    public static void ConfigureDrnApplication(WebApplication application)
    {
        application.Services.ValidateServicesAddedByAttributes();
        application.UseForwardedHeaders();
        application.UseMiddleware<HttpScopeLogger>();
        application.UseHostFiltering();
        application.UseMiddleware<HttpRequestLogger>();

        if (application.Environment.IsDevelopment())
        {
            application.UseSwagger();
            application.UseSwaggerUI();
        }

        application.UseRouting();

        application.UseAuthorization();
        application.MapControllers();
    }

    public static WebApplicationBuilder ConfigureDrnApplicationBuilder<TProgram>(WebApplicationBuilder builder)
        where TProgram : DrnProgramBase<TProgram>, IDrnProgram, new()
    {
        builder.Host.UseSerilog();
        builder.WebHost.UseKestrelCore().ConfigureKestrel(kestrelServerOptions =>
            kestrelServerOptions.Configure(builder.Configuration.GetSection("Kestrel"), true));
        AddDefaultServices<TProgram>(builder.Services, builder.Configuration);

        return builder;
    }

    public static void AddDefaultServices<TProgram>(IServiceCollection services, IConfiguration configuration)
        where TProgram : DrnProgramBase<TProgram>, IDrnProgram, new()
    {
        services.ConfigureHttpJsonOptions(options => JsonConventions.SetJsonDefaults(options.SerializerOptions));
        services.AddLogging(logging => logging.Configure(options
            => options.ActivityTrackingOptions = ActivityTrackingOptions.SpanId |
                                                 ActivityTrackingOptions.TraceId |
                                                 ActivityTrackingOptions.ParentId));

        var mvcBuilder = services.AddControllers()
            .AddJsonOptions(options => JsonConventions.SetJsonDefaults(options.JsonSerializerOptions));

        var programAssembly = typeof(TProgram).Assembly;
        var partName = typeof(TProgram).GetAssemblyName();
        var applicationParts = mvcBuilder.PartManager.ApplicationParts;
        var controllersAdded = applicationParts.Any(p => p.Name == partName);
        if (!controllersAdded) mvcBuilder.AddApplicationPart(programAssembly);

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        services.Configure<ForwardedHeadersOptions>(options => { options.ForwardedHeaders = ForwardedHeaders.All; });

        services.PostConfigure<HostFilteringOptions>(options =>
        {
            if (options.AllowedHosts != null && options.AllowedHosts.Count != 0) return;
            var separator = new[] { ';' };
            // "AllowedHosts": "localhost;127.0.0.1;[::1]"
            var hosts = configuration["AllowedHosts"]?.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            // Fall back to "*" to disable.
            options.AllowedHosts = hosts?.Length > 0 ? hosts : ["*"];
        });
        // Change notification
        services.AddSingleton<IOptionsChangeTokenSource<HostFilteringOptions>>(
            new ConfigurationChangeTokenSource<HostFilteringOptions>(configuration));
    }
}