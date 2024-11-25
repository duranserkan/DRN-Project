using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Design;

namespace DRN.Framework.EntityFramework.Context;

/// <summary>
/// Overrides default migration location with context specific location
/// </summary>
/// <param name="dependencies"></param>
public class DrnMigrationsScaffolder(MigrationsScaffolderDependencies dependencies) : MigrationsScaffolder(dependencies)
{
    public override MigrationFiles Save(string projectDir, ScaffoldedMigration migration, string? outputDir, bool dryRun)
    {
        if (string.IsNullOrEmpty(outputDir))
        {
            var relativeNamespace = GetRelativeNamespace(Dependencies.CurrentContext.Context);
            Console.WriteLine($"relative namespace: {relativeNamespace}");
            var relativePathOfDbContext = Path.Combine(relativeNamespace.Split('.'));
            Console.WriteLine($"relative path of DbContext: {relativePathOfDbContext}");
            outputDir = Path.Combine(relativePathOfDbContext, "Migrations");
            Console.WriteLine($"relative output dir: {outputDir}");
        }

        return base.Save(projectDir, migration, outputDir, dryRun);
    }

    public override ScaffoldedMigration ScaffoldMigration(string migrationName, string? rootNamespace, string? subNamespace = null, string? language = null, bool dryRun = false)
    {
        var relativeNamespaceForMigration = $"{GetRelativeNamespace(Dependencies.CurrentContext.Context)}.Migrations";
        return base.ScaffoldMigration(migrationName, rootNamespace, subNamespace ?? relativeNamespaceForMigration, language, dryRun);
    }

    private static string GetRelativeNamespace(DbContext dbContext)
    {
        var dbContextType = dbContext.GetType();
        var assemblyName = dbContextType.Assembly.GetName().Name!;

        return dbContextType.Namespace!.Remove(0, assemblyName.Length).TrimStart('.');
    }
}