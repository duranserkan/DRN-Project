using DRN.Framework.EntityFramework.Context;

namespace Sample.Infra.Repositories.QA;

public class QAContextFactory : DesignTimeDbContextFactory<QAContext>
{
    //for ef tools
    //dotnet tool install --global dotnet-ef
    //dotnet tool update
    //https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation?tabs=dotnet-core-cli
    //from project root
    //dotnet ef migrations add --project Sample.Infrastructure.csproj --context Sample.Infrastructure.Repositories.QA.QAContext [MigrationName] --output-dir Repositories/QA/Migrations
    //dotnet ef database update --project Sample.Infrastructure.csproj --context Sample.Infrastructure.Repositories.QA.QAContext  -- "Server=localhost;Port=5432;Database=sample;User Id=postgres;Password=postgres;"
}