using System.Reflection;
using DRN.Framework.EntityFramework.Context;
using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace DRN.Framework.EntityFramework.Extensions;

public static class ServiceCollectionExtensions
{
    //https://stackoverflow.com/questions/60047465/more-than-twenty-iserviceprovider-instances-have-been-created-for-internal-use
    //https://github.com/dotnet/efcore/issues/29330
    //https://github.com/dotnet/efcore/issues/12927
    public static void AddDbContextWithConventions<TContext>(this IServiceCollection sc) where TContext : DbContext
    {
        var contextName = typeof(TContext).Name;

        //todo: check multiplexing and MultiHost usability
        //https://www.npgsql.org/doc/failover-and-load-balancing.html
        //https://www.npgsql.org/doc/basic-usage.html
        sc.AddNpgsqlDataSource("", (serviceProvider, dataSourceBuilderBuilder) =>
        {
            var appSettings = serviceProvider.GetRequiredService<IAppSettings>();
            var connectionString = appSettings.Environment == AppEnvironment.Development
                ? DrnContextDevelopmentConnection.GetConnectionString(appSettings, contextName)
                : appSettings.GetRequiredConnectionString(contextName);
            var attributes = DbContextConventions.GetAttributesFromCache<TContext>();

            dataSourceBuilderBuilder.ConnectionStringBuilder.ConnectionString = connectionString;
            foreach (var attribute in attributes)
                attribute.ConfigureNpgsqlDataSource<TContext>(dataSourceBuilderBuilder);
        }, serviceKey: contextName);

        sc.AddDbContext<TContext>((serviceProvider, optionsBuilder) =>
        {
            var dataSource = serviceProvider.GetRequiredKeyedService<NpgsqlDataSource>(contextName);
            DbContextConventions.UpdateDbContextOptionsBuilder<TContext>(dataSource, optionsBuilder);
        });
    }

    public static void AddDbContextsWithConventions(this IServiceCollection sc, Assembly? assembly)
    {
        assembly ??= Assembly.GetCallingAssembly();
        var contextTypes = assembly.GetTypesAssignableTo(typeof(DbContext));

        foreach (var contextType in contextTypes)
            typeof(ServiceCollectionExtensions).MakeGenericMethod(nameof(AddDbContextWithConventions), contextType).Invoke(null, [sc]);
    }
}