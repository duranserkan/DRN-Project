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

    public virtual void ConfigureDbContextOptions<TContext>(DbContextOptionsBuilder builder) where TContext : DbContext
    {
    }
}