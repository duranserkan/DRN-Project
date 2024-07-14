using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace DRN.Framework.EntityFramework.Attributes;

public abstract class NpgsqlDbContextOptionsAttribute : Attribute
{
    internal bool FrameworkDefined = false;

    public virtual void ConfigureNpgsqlOptions<TContext>(NpgsqlDbContextOptionsBuilder builder) where TContext : DbContext
    {
    }

    public virtual void ConfigureNpgsqlDataSource<TContext>(NpgsqlDataSourceBuilder builder) where TContext : DbContext
    {
    }

    /// <summary>
    /// Configures db context options when the context registered with DrnContextServiceRegistrationAttribute
    /// </summary>
    /// <param name="builder">the builder</param>
    /// <param name="serviceProvider">IServiceProvider won't be available at design time factory</param>
    /// <typeparam name="TContext">the context registered with DrnContextServiceRegistrationAttribute</typeparam>
    public virtual void ConfigureDbContextOptions<TContext>(DbContextOptionsBuilder builder, IServiceProvider? serviceProvider) where TContext : DbContext
    {
    }
}