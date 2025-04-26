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

        category1.Id.Should().Be(0);
        category1.IsPendingInsert.Should().BeTrue();
        
        category2.Id.Should().Be(0);
        category2.IsPendingInsert.Should().BeTrue();
        
        qaContext.Categories.Add(category1);
        qaContext.Categories.Add(category2);
        
        category1.Id.Should().BeNegative();
        category1.IsPendingInsert.Should().BeTrue();
        
        category2.Id.Should().BeNegative();
        category2.IsPendingInsert.Should().BeTrue();
        
        var id1= category1.Id;
        var id2 = category2.Id;
        
        await qaContext.SaveChangesAsync();
        
        category1.Id.Should().Be(id1);
        category1.IsPendingInsert.Should().BeFalse();
        
        category2.Id.Should().Be(id2);
        category2.IsPendingInsert.Should().BeFalse();
    }
}