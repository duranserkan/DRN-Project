﻿using System.Text.Json.Serialization;
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
    private static WebApplication CreateApp(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var serviceCollection = builder.Services;
        var configuration = builder.Configuration;
        configuration.AddMountDirectorySettings();

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