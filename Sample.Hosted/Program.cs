using Sample.Application;
using Sample.Infra;
//https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host
//https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/program-structure/top-level-statements
//https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation
//https://learn.microsoft.com/en-us/aspnet/core/migration/50-to-60 new hosting model
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

static WebApplication CreateApp(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    var configuration = builder.Configuration;
    var serviceCollection = builder.Services;
    AddServices(serviceCollection, configuration);

    var app = builder.Build();
    app.Services.ValidateServicesAddedByAttributes();

    ConfigureApp(app);

    return app;
}

static void AddServices(IServiceCollection services, IConfiguration configuration)
{
    services.AddControllers();
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen();
    services.AddSampleInfraServices();
    services.AddSampleApplicationServices();
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

public partial class Program
{
}