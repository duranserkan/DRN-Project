using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.Utils.Settings;
using Microsoft.OpenApi.Models;

namespace DRN.Nexus.Hosted;

public static class NexusModule
{
    public static IServiceCollection AddNexusServices(this IServiceCollection services, IAppSettings appSettings)
    {
        services.AddServicesWithAttributes();
        if (appSettings.Environment != AppEnvironment.Development) return services;

        services.AddSwaggerGen(opt =>
        {
            opt.SwaggerDoc("v1", new OpenApiInfo { Title = "MyAPI", Version = "v1" });
            opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                In = ParameterLocation.Header,
                Description = "Please enter token",
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer"
            });

            opt.AddSecurityRequirement(new OpenApiSecurityRequirement
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

        return services;
    }
}