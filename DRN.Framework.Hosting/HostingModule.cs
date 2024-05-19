using DRN.Framework.Utils.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Hosting;

public static class HostingModule
{
    public static IServiceCollection AdDrnHosting(this IServiceCollection sc)
    {
        sc.AddServicesWithAttributes();

        return sc;
    }
}