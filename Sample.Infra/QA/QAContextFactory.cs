using DRN.Framework.EntityFramework.Context;

namespace Sample.Infra.QA;

public class QAContextFactory : DesignTimeDbContextFactory<QAContext>
{
    //for ef tools
    //dotnet tool install --global dotnet-ef
    //dotnet tool update
    //https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation?tabs=dotnet-core-cli
    //from project root
    //dotnet ef migrations add --context QAContext [MigrationName]
    //dotnet ef database update --context QAContext  -- "connectionString"
}