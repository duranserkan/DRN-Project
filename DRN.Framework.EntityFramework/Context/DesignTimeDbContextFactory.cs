using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.EntityFramework.Context;

public class DesignTimeDbContextFactory<TContext> : IDesignTimeDbContextFactory<TContext>, IDesignTimeServices
    where TContext : DbContext

{
    //for ef tools
    //dotnet tool install --global dotnet-ef
    //dotnet tool update
    //https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation?tabs=dotnet-core-cli
    //from example project root
    //dotnet ef migrations add --project Sample.Infra.csproj --context Sample.Infra.Repositories.QA.QAContext [MigrationName]
    //dotnet ef database update --project Sample.Infra.csproj --context Sample.Infra.Repositories.QA.QAContext  -- "connectionString"
    public TContext CreateDbContext(string[] args)
    {
        var contextName = typeof(TContext).Name;
        var connectionString = args.FirstOrDefault()!;
        var optionsBuilder = DbContextConventions.DbContextGetOptionsBuilder<TContext>(connectionString, contextName);

        return (TContext)Activator.CreateInstance(typeof(TContext), optionsBuilder.Options)!;
    }

    public void ConfigureDesignTimeServices(IServiceCollection serviceCollection) =>
        serviceCollection.AddSingleton<IMigrationsScaffolder, DrnMigrationsScaffolder>();
}

/// <summary>
/// Overrides default migration location with context specific location
/// </summary>
/// <param name="dependencies"></param>
public class DrnMigrationsScaffolder(MigrationsScaffolderDependencies dependencies) : MigrationsScaffolder(dependencies)
{
    public override MigrationFiles Save(string projectDir, ScaffoldedMigration migration, string? outputDir)
    {
        if (string.IsNullOrEmpty(outputDir))
        {
            var dbContext = Dependencies.CurrentContext.Context.GetType();
            var assemblyName = dbContext.Assembly.GetName().Name!;

            var relativeNamespace = dbContext.Namespace!.Remove(0, assemblyName.Length).TrimStart('.');
            Console.WriteLine($"relative namespace: {relativeNamespace}");
            var relativePathOfDbContext = Path.Combine(relativeNamespace.Split('.'));
            Console.WriteLine($"relative path of DbContext: {relativePathOfDbContext}");
            outputDir = Path.Combine(relativePathOfDbContext, "Migrations");
            Console.WriteLine($"relative output dir: {outputDir}");
        }

        return base.Save(projectDir, migration, outputDir);
    }
}