using System.Reflection;
using DRN.Framework.Utils.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.EntityFramework.Context;

public static class ServiceCollectionExtensions
{
    public static void AddDbContextWithConventions<TContext>(this IServiceCollection sc, IConfiguration configuration) where TContext : DbContext
    {
        var name = typeof(TContext).Name;
        var connectionString = configuration.GetConnectionString(name)!;
        sc.AddDbContext<TContext>(options => DbContextConventions.DbContextGetOptionsBuilder<TContext>(connectionString, name, options));
    }

    public static void AddDbContextsWithConventions(this IServiceCollection sc, IConfiguration configuration, Assembly? assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();
        var contextTypes = assembly.GetTypesAssignableTo(typeof(DbContext));

        foreach (var contextType in contextTypes)
            typeof(ServiceCollectionExtensions)
                .MakeGenericMethod(nameof(AddDbContextWithConventions), contextType)
                .Invoke(null, new object[] { sc, configuration });
    }
}