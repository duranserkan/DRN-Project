using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DRN.Framework.EntityFramework.Context;

public abstract class DesignTimeDbContextFactory<TContext> : IDesignTimeDbContextFactory<TContext> where TContext : DbContext
{
    //for ef tools
    //dotnet tool install --global dotnet-ef
    //dotnet tool update
    //https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation?tabs=dotnet-core-cli
    //from example project root
    //dotnet ef migrations add --project Sample.Infrastructure.csproj --context Sample.Infrastructure.Repositories.QA.QAContext [MigrationName] --output-dir Repositories/QA/Migrations
    //dotnet ef database update --project Sample.Infrastructure.csproj --context Sample.Infrastructure.Repositories.QA.QAContext  -- "Server=localhost;Port=5432;Database=sample;User Id=postgres;Password=postgres;"
    public TContext CreateDbContext(string[] args)
    {
        var contextName = typeof(TContext).Name;
        var connectionString = args.FirstOrDefault()!;
        var optionsBuilder = DbContextConventions.DbContextGetOptionsBuilder<TContext>(connectionString, contextName);

        return (TContext)Activator.CreateInstance(typeof(TContext), optionsBuilder.Options)!;
    }
}