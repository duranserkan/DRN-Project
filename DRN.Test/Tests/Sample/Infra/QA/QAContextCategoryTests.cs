using Sample.Domain.QA.Categories;
using Sample.Infra;
using Sample.Infra.QA;

namespace DRN.Test.Tests.Sample.Infra.QA;

public class QAContextCategoryTests
{
    [Theory]
    [DataInline]
    public async Task QAContext_Should_Have_Categories(TestContext context)
    {
        context.ServiceCollection.AddSampleInfraServices();
        await context.ContainerContext.Postgres.ApplyMigrationsAsync();
        var qaContext = context.GetRequiredService<QAContext>();
        
        var category1 = new Category("first category");
        var category2 = new Category("second category");
        
        qaContext.Categories.Add(category1);
        qaContext.Categories.Add(category2);
        
        await qaContext.SaveChangesAsync();
    }

}