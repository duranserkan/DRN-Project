using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Sample.Domain.Users;
using Sample.Infra.Identity;
using Sample.Utils;

namespace Sample.Infra;

public static class InfraModule
{
    public static IServiceCollection AddSampleInfraServices(this IServiceCollection sc)
    {
        sc.AddServicesWithAttributes();
        sc.AddSampleUtils();

        sc.AddIdentityCore<SampleUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<SampleIdentityContext>();

        return sc;
    }
}