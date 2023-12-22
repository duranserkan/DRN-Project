using System.Reflection;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.EntityFramework.Context;

public static class ServiceCollectionExtensions
{
    public static void AddDbContextWithConventions<TContext>(this IServiceCollection sc) where TContext : DbContext
    {
        sc.AddDbContext<TContext>((serviceProvider, optionsBuilder) =>
        {
            var name = typeof(TContext).Name;
            var connectionString = serviceProvider.GetRequiredService<IAppSettings>().GetRequiredConnectionString(name);
            DbContextConventions.UpdateDbContextOptionsBuilder<TContext>(connectionString, name, optionsBuilder);
        });
    }

    public static void AddDbContextsWithConventions(this IServiceCollection sc, Assembly? assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();
        var contextTypes = assembly.GetTypesAssignableTo(typeof(DbContext));

        foreach (var contextType in contextTypes)
            typeof(ServiceCollectionExtensions).MakeGenericMethod(nameof(AddDbContextWithConventions), contextType).Invoke(null, [sc]);
    }
}