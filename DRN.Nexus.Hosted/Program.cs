﻿using DRN.Nexus.Application;
using DRN.Nexus.Infra;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace DRN.Nexus.Hosted;

public class Program
{
    public static void Main(string[] args)
    {
        try
        {
            var app = CreateApp(args);
            //log info
            app.Run();
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

    //https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host
    //https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/program-structure/top-level-statements
    //https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation
    //https://learn.microsoft.com/en-us/aspnet/core/migration/50-to-60 new hosting model
    static WebApplication CreateApp(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var configuration = builder.Configuration;
        var serviceCollection = builder.Services;

        configuration.AddKeyPerFile("/config", true);
        AddServices(serviceCollection);

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ConfigureEndpointDefaults(listenOptions => { listenOptions.Protocols = HttpProtocols.Http2; });
        });

        var app = builder.Build();
        app.Services.ValidateServicesAddedByAttributes();

        ConfigureApp(app);

        return app;
    }

    static void AddServices(IServiceCollection services)
    {
        services.AddControllers();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddNexusInfraServices();
        services.AddNexusApplicationServices();
    }

    static void ConfigureApp(WebApplication webApplication)
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