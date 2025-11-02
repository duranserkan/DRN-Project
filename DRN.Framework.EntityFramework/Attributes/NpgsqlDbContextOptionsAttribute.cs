using DRN.Framework.EntityFramework.Context;
using DRN.Framework.Utils.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace DRN.Framework.EntityFramework.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public abstract class NpgsqlDbContextOptionsAttribute : Attribute
{
    internal bool FrameworkDefined = false;

    /// <summary>
    /// Override this method to configure Npgsql specific DbContext options when the context registered with <see cref="DrnContextServiceRegistrationAttribute"/>
    /// </summary>
    /// <param name="builder">the NpgsqlDbContextOptionsBuilder</param>
    /// <param name="serviceProvider">IServiceProvider won't be available when called by design time factory make sure your method override works without it</param>
    /// <typeparam name="TContext">The context registered with DrnContextServiceRegistrationAttribute</typeparam>
    public virtual void ConfigureNpgsqlOptions<TContext>(NpgsqlDbContextOptionsBuilder builder, IServiceProvider? serviceProvider) where TContext : DbContext
    {
    }

    /// <summary>
    /// Override this method to configure NpgsqlDataSource when the context registered with <see cref="DrnContextServiceRegistrationAttribute"/>
    /// </summary>
    /// <param name="builder">the NpgsqlDataSourceBuilder</param>
    /// <param name="serviceProvider">IServiceProvider won't be available when called by design time factory make sure your method override works without it</param>
    /// <typeparam name="TContext">The context registered with DrnContextServiceRegistrationAttribute</typeparam>
    public virtual void ConfigureNpgsqlDataSource<TContext>(NpgsqlDataSourceBuilder builder, IServiceProvider serviceProvider) where TContext : DbContext
    {
    }

    /// <summary>
    /// Override this method to configures DbContext options when the context registered with <see cref="DrnContextServiceRegistrationAttribute"/>
    /// </summary>
    /// <param name="builder">the DbContextOptionsBuilder</param>
    /// <param name="serviceProvider">IServiceProvider won't be available when called by design time factory make sure your method override works without it</param>
    /// <typeparam name="TContext">The context registered with DrnContextServiceRegistrationAttribute</typeparam>
    public virtual void ConfigureDbContextOptions<TContext>(DbContextOptionsBuilder builder, IServiceProvider? serviceProvider) where TContext : DbContext
    {
        if (UsePrototypeMode)
            builder.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
    }

    public virtual Task SeedAsync(IServiceProvider serviceProvider, IAppSettings appSettings) => Task.CompletedTask;
    
    /// <summary>
    /// When <see cref="UsePrototypeMode"/> is enabled and 
    /// <see cref="DrnDevelopmentSettings.LaunchExternalDependencies"/> is <see langword="true"/>, 
    /// the <see cref="DbContext"/> uses a separate, throwaway database created using Testcontainers.
    /// 
    /// This database is recreated only if all the following conditions are met:
    /// <list type="number">
    ///   <item>
    ///     <description>There are pending model changes not yet reflected in a migration.</description>
    ///   </item>
    ///   <item>
    ///     <description><see cref="UsePrototypeMode"/> is enabled.</description>
    ///   </item>
    ///   <item>
    ///     <description>The prototype flag in <see cref="DrnDevelopmentSettings"/> is enabled.</description>
    ///   </item>
    /// </list>
    /// 
    /// If the prototype flag in <see cref="DrnDevelopmentSettings"/> is disabled, the database is never recreated—
    /// even if <see cref="UsePrototypeMode"/> is enabled and model changes are detected.
    /// </summary>
    public virtual bool UsePrototypeMode { get; set; } = false;

    public virtual bool UsePrototypeModeWhenMigrationExists { get; set; } = false;
}