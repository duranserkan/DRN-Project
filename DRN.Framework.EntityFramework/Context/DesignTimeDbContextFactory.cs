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
    //dotnet ef migrations add --project Sample.Infra.csproj --context Sample.Infra.Repositories.QA.QAContext --output-dir Repositories/QA/Migrations [MigrationName]
    //dotnet ef database update --project Sample.Infra.csproj --context Sample.Infra.Repositories.QA.QAContext  -- "connectionString"
    public TContext CreateDbContext(string[] args)
    {
        var contextName = typeof(TContext).Name;
        var connectionString = args.FirstOrDefault()!;
        var optionsBuilder = DbContextConventions.DbContextGetOptionsBuilder<TContext>(connectionString, contextName);

        return (TContext)Activator.CreateInstance(typeof(TContext), optionsBuilder.Options)!;
    }
}