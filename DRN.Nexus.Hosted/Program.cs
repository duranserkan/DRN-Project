using DRN.Framework.Utils.DependencyInjection;
using DRN.Nexus.Application;
using DRN.Nexus.Infra;

try
{
    var app = CreateApp(args);
    //log info
    app.Run();
}
catch (Exception exception)
{
    //log error
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

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();

    return app;
}

static void AddServices(IServiceCollection services, IConfiguration configuration)
{
    services.AddControllers();
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen();
    services.AddInfraServices();
    services.AddApplicationServices();
}