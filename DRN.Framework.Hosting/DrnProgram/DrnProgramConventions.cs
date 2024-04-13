using DRN.Framework.Hosting.Middlewares;
using DRN.Framework.SharedKernel.Conventions;
using DRN.Framework.Utils.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace DRN.Framework.Hosting.DrnProgram;

public static class DrnProgramConventions
{
    public static WebApplicationBuilder GetApplicationBuilder<TProgram>(WebApplicationOptions options, DrnAppBuilderType drnAppBuilderType)
        where TProgram : DrnProgramBase<TProgram>, IDrnProgram, new()
    {
        var builder = drnAppBuilderType switch
        {
            DrnAppBuilderType.Empty => WebApplication.CreateEmptyBuilder(options),
            DrnAppBuilderType.Slim => WebApplication.CreateSlimBuilder(options),
            DrnAppBuilderType.DrnDefaults => WebApplication.CreateSlimBuilder(options),
            DrnAppBuilderType.Default => WebApplication.CreateBuilder(options),
            _ => throw new ArgumentOutOfRangeException()
        };

        return builder;
    }

    public static WebApplicationBuilder ConfigureDrnApplicationBuilder<TProgram>(WebApplicationBuilder builder)
        where TProgram : DrnProgramBase<TProgram>, IDrnProgram, new()
    {
        builder.Host.UseSerilog();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ConfigureEndpointDefaults(listenOptions => { listenOptions.Protocols = HttpProtocols.Http2; });
        });

        AddDefaultServices<TProgram>(builder.Services);

        return builder;
    }

    public static void AddDefaultServices<TProgram>(IServiceCollection services)
        where TProgram : DrnProgramBase<TProgram>, IDrnProgram, new()
    {
        services.ConfigureHttpJsonOptions(options => JsonConventions.SetJsonDefaults(options.SerializerOptions));
        var mvcBuilder = services.AddControllers()
            .AddJsonOptions(options => JsonConventions.SetJsonDefaults(options.JsonSerializerOptions));

        var programAssembly = typeof(TProgram).Assembly;
        var partName = programAssembly.GetName().Name;
        var applicationParts = mvcBuilder.PartManager.ApplicationParts;
        var controllersAdded = applicationParts.Any(p => p.Name == partName);
        if (!controllersAdded) mvcBuilder.AddApplicationPart(programAssembly);

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
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
}