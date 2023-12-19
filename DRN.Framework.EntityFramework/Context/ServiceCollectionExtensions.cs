using System.Reflection;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.EntityFramework.Context;

public static class ServiceCollectionExtensions
{
    public static void AddDbContextWithConventions<TContext>(this IServiceCollection sc) where TContext : DbContext
    {
        var name = typeof(TContext).Name;
        sc.AddDbContext<TContext>((serviceProvider, optionsBuilder) =>
        {
            var appSettings = serviceProvider.GetRequiredService<IAppSettings>();
            var connectionString = appSettings.GetRequiredConnectionString(name);
            DbContextConventions.DbContextGetOptionsBuilder<TContext>(connectionString, name, optionsBuilder);
        });
    }

    public static void AddDbContextsWithConventions(this IServiceCollection sc, Assembly? assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();
        var contextTypes = assembly.GetTypesAssignableTo(typeof(DbContext));

        foreach (var contextType in contextTypes)
            typeof(ServiceCollectionExtensions)
                .MakeGenericMethod(nameof(AddDbContextWithConventions), contextType)
                .Invoke(null, new object[] { sc });
    }
}