using System.Reflection;
using DRN.Framework.Hosting.Middlewares;
using DRN.Framework.SharedKernel.Conventions;
using DRN.Framework.Utils.DependencyInjection;
using DRN.Framework.Utils.Extensions;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
        application.UseMiddleware<HttpScopeLogger>();
        application.UseMiddleware<HttpRequestLogger>();

        if (application.Environment.IsDevelopment())
        {
            application.UseSwagger();
            application.UseSwaggerUI();
        }

        application.UseAuthorization();
        application.MapControllers();
    }

    public static WebApplicationBuilder ConfigureDrnApplicationBuilder<TProgram>(WebApplicationBuilder builder)
        where TProgram : DrnProgramBase<TProgram>, IDrnProgram, new()
    {
        builder.Host.UseSerilog();
        builder.WebHost
            .UseKestrelCore()
            .ConfigureKestrel(kestrelServerOptions =>
            {
                kestrelServerOptions.Configure(builder.Configuration.GetSection("Kestrel"), true);
                kestrelServerOptions.ConfigureEndpointDefaults(listenOptions => listenOptions.Protocols = HttpProtocols.Http2);
            });

        var ConfigureWebDefaultsWorker = typeof(WebHost)
            .GetMethod("ConfigureWebDefaultsWorker", BindingFlags.Static | BindingFlags.NonPublic);
        ConfigureWebDefaultsWorker?.Invoke(null, [builder.WebHost, null]);

        AddDefaultServices<TProgram>(builder.Services, builder.Configuration);

        return builder;
    }

    public static void AddDefaultServices<TProgram>(IServiceCollection services, IConfiguration configuration)
        where TProgram : DrnProgramBase<TProgram>, IDrnProgram, new()
    {
        services.ConfigureHttpJsonOptions(options => JsonConventions.SetJsonDefaults(options.SerializerOptions));
        services.AddLogging(logging =>
        {
            logging.AddConfiguration(configuration.GetSection("Logging"));
            logging.AddSimpleConsole();
            logging.Configure(options => options.ActivityTrackingOptions = ActivityTrackingOptions.SpanId |
                                                                           ActivityTrackingOptions.TraceId |
                                                                           ActivityTrackingOptions.ParentId);
        });


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
    }
}