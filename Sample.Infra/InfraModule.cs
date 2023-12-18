using DRN.Framework.EntityFramework.Context;
using DRN.Framework.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Sample.Infra;

public static class InfraModule
{
    public static IServiceCollection AddSampleInfraServices(this IServiceCollection sc, IConfiguration configuration)
    {
        sc.AddDrnUtils();
        sc.AddServicesWithAttributes();
        sc.AddDbContextsWithConventions(configuration);

        return sc;
    }
}