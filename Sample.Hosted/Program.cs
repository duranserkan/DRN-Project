using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using DRN.Framework.Hosting.Extensions;
using DRN.Framework.SharedKernel.Conventions;
using Sample.Application;
using Sample.Infra;

namespace Sample.Hosted;

public class Program
{
    public static void Main(string[] args)
    {
        try
        {
            _ = JsonConventions.DefaultOptions;
            var app = CreateApp(args);
            //log info
            app.Run();
        }
        catch (Exception exception)
        {
            Console.WriteLine("UpsertException Type:");
            Console.WriteLine(exception.GetType().FullName);
            Console.WriteLine("UpsertException Message:");
            Console.WriteLine(exception.Message);
            Console.WriteLine("UpsertException StackTrace:");
            Console.WriteLine(exception.StackTrace);
        }
        finally
        {
            //log shutdown
        }
    }

    //https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host
    //https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/program-structure/top-level-statements
    //https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation
    //https://learn.microsoft.com/en-us/aspnet/core/migration/50-to-60 new hosting model
    private static WebApplication CreateApp(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var serviceCollection = builder.Services;
        var configuration = builder.Configuration;
        configuration.AddMountDirectorySettings();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ConfigureEndpointDefaults(listenOptions => { listenOptions.Protocols = HttpProtocols.Http2; });
        });

        AddServices(serviceCollection);
        var app = builder.Build();
        app.Services.ValidateServicesAddedByAttributes();

        ConfigureApp(app);

        return app;
    }

    private static void AddServices(IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.ConfigureHttpJsonOptions(options =>
        {
            var converter = new JsonStringEnumConverter();
            options.SerializerOptions.Converters.Add(converter);
            options.SerializerOptions.PropertyNameCaseInsensitive = true;
        });

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddSampleInfraServices();
        services.AddSampleApplicationServices();
    }

    private static void ConfigureApp(WebApplication webApplication)
    {
        if (webApplication.Environment.IsDevelopment())
        {
            webApplication.UseSwagger();
            webApplication.UseSwaggerUI();
        }

        //webApplication.UseHttpsRedirection();
        webApplication.UseAuthorization();
        webApplication.MapControllers();
    }
}