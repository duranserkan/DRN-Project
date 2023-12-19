using DRN.Framework.EntityFramework.Context;

namespace Sample.Infra.Repositories.QA;

public class QAContextFactory : DesignTimeDbContextFactory<QAContext>
{
    //for ef tools
    //dotnet tool install --global dotnet-ef
    //dotnet tool update
    //https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation?tabs=dotnet-core-cli
    //from project root
    //dotnet ef migrations add --project Sample.Infrastructure.csproj --context Sample.Infrastructure.Repositories.QA.QAContext --output-dir Repositories/QA/Migrations [MigrationName]
    //dotnet ef database update --project Sample.Infrastructure.csproj --context Sample.Infrastructure.Repositories.QA.QAContext  -- "connectionString"
}