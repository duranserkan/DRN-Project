using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace DRN.Framework.EntityFramework.Context;

public abstract class NpgsqlDbContextOptionsAttribute : Attribute
{
    internal bool FrameworkDefined = false;

    public virtual void ConfigureNpgsqlOptions(NpgsqlDbContextOptionsBuilder builder)
    {
    }

    public virtual void ConfigureNpgsqlDataSource(NpgsqlDataSourceBuilder builder)
    {
    }

    public virtual void ConfigureDbContextOptions(DbContextOptionsBuilder builder)
    {
    }
}

public class SplitQueryAttribute : NpgsqlDbContextOptionsAttribute
{
    public SplitQueryAttribute() => FrameworkDefined = true;

    public override void ConfigureNpgsqlOptions(NpgsqlDbContextOptionsBuilder builder)
        => builder.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
}