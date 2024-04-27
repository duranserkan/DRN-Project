using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Testing.Contexts;
using DRN.Nexus.Application;
using DRN.Nexus.Infra;
using DRN.Nexus.Infra.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.OpenApi.Models;

namespace DRN.Nexus.Hosted;

public class Program : DrnProgramBase<Program>, IDrnProgram
{
    public static async Task Main(string[] args) => await RunAsync(args);

    protected override async Task AddServicesAsync(WebApplicationBuilder builder)
    {
        builder.Services
            .AddNexusInfraServices()
            .AddNexusApplicationServices()
            .AddAuthorization()
            .AddIdentityApiEndpoints<IdentityUser>()
            .AddEntityFrameworkStores<NexusIdentityContext>();

        builder.Services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                In = ParameterLocation.Header,
                Description = "Please enter token",
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                BearerFormat = "JWT",
                Scheme = "bearer"
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        await LaunchContext.LaunchExternalDependenciesAsync(builder, AppSettings);
    }

    protected override void MapApplicationEndpoints(WebApplication application)
    {
        if (AppSettings.IsDevEnvironment)
        {
            application.MapSwagger();
            application.UseSwaggerUI();
        }

        application.MapGroup("/identity").MapIdentityApi<IdentityUser>();
        
        base.MapApplicationEndpoints(application);
    }
}