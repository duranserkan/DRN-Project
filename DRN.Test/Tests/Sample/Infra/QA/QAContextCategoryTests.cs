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

        (category1 == category2).Should().BeFalse();

        category1.Id.Should().Be(0);
        category1.IsPendingInsert.Should().BeTrue();

        category2.Id.Should().Be(0);
        category2.IsPendingInsert.Should().BeTrue();

        qaContext.Categories.Add(category1);
        qaContext.Categories.Add(category2);

        (category1 == category2).Should().BeFalse();

        category1.Id.Should().BeNegative();
        category1.IsPendingInsert.Should().BeTrue();

        category2.Id.Should().BeNegative();
        category2.IsPendingInsert.Should().BeTrue();

        var id1 = category1.Id;
        var id2 = category2.Id;

        await qaContext.SaveChangesAsync();

        category1.Id.Should().Be(id1);
        category1.IsPendingInsert.Should().BeFalse();

        category2.Id.Should().Be(id2);
        category2.IsPendingInsert.Should().BeFalse();

        //to prevent dbcontext cache, return the same instance to test equality based on EntityId
        using var scope = context.CreateScope();
        var scopedQaContext = scope.ServiceProvider.GetRequiredService<QAContext>();

        var categoryFromDb1 = await scopedQaContext.Categories.FindAsync(category1.Id);
        categoryFromDb1.Should().NotBeNull();
        categoryFromDb1.Name.Should().Be(category1.Name);

        var categoryFromDb2 = await scopedQaContext.Categories.FindAsync(category2.Id);
        categoryFromDb2.Should().NotBeNull();
        categoryFromDb2.Name.Should().Be(category2.Name);

        (category1 == categoryFromDb1).Should().BeTrue();
        ReferenceEquals(category1, categoryFromDb1).Should().BeFalse();
        
        (category2 == categoryFromDb2).Should().BeTrue();
        ReferenceEquals(category2, categoryFromDb2).Should().BeFalse();
        
        (category1 == category2).Should().BeFalse();
        ReferenceEquals(category1, category2).Should().BeFalse();
        
        (categoryFromDb1 == categoryFromDb2).Should().BeFalse();
        ReferenceEquals(categoryFromDb1, categoryFromDb2).Should().BeFalse();
    }
}