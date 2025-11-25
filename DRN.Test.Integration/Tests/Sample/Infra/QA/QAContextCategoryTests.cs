using Sample.Domain.QA.Categories;
using Sample.Infra;
using Sample.Infra.QA;

namespace DRN.Test.Integration.Tests.Sample.Infra.QA;

public class QAContextCategoryTests
{
    [Theory]
    [DataInline]
    public async Task QAContext_Should_Have_Categories(DrnTestContext context)
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

        var category1FromDb2 = await scopedQaContext.Categories.FindAsync(category1.Id);
        category1FromDb2.Should().NotBeNull();
        category1FromDb2.Name.Should().Be(category1.Name);

        var category2FromDb2 = await scopedQaContext.Categories.FindAsync(category2.Id);
        category2FromDb2.Should().NotBeNull();
        category2FromDb2.Name.Should().Be(category2.Name);

        (category1 == category1FromDb2).Should().BeTrue();
        ReferenceEquals(category1, category1FromDb2).Should().BeFalse();

        (category2 == category2FromDb2).Should().BeTrue();
        ReferenceEquals(category2, category2FromDb2).Should().BeFalse();

        (category1 == category2).Should().BeFalse();
        (category1 != category2).Should().BeTrue();
        ReferenceEquals(category1, category2).Should().BeFalse();

        (category1FromDb2 == category2FromDb2).Should().BeFalse();
        (category1FromDb2 != category2FromDb2).Should().BeTrue();
        ReferenceEquals(category1FromDb2, category2FromDb2).Should().BeFalse();

        category2.CompareTo(category1).Should().Be(1);
        category1.CompareTo(category2).Should().Be(-1);
        category1.CompareTo(null).Should().Be(1);
        category1.CompareTo(category1FromDb2).Should().Be(0);

        (category2 > category1).Should().BeTrue();
        (category1 < category2).Should().BeTrue();
        (category2 >= category1).Should().BeTrue();
        (category1 <= category2).Should().BeTrue();

        (category2 < category1).Should().BeFalse();
        (category1 > category2).Should().BeFalse();
        (category2 <= category1).Should().BeFalse();
        (category1 >= category2).Should().BeFalse();

        (category2 >= category2FromDb2).Should().BeTrue();
        (category2 <= category2FromDb2).Should().BeTrue();


        qaContext.Categories.Remove(category1);
        qaContext.Categories.Remove(category2);
        await qaContext.SaveChangesAsync();

        category1 = await qaContext.Categories.FindAsync(category1.Id);
        category1.Should().BeNull();
        category2 = await qaContext.Categories.FindAsync(category2.Id);
        category2.Should().BeNull();

        var category3 = new Category("second category");
        qaContext.Categories.Add(category3);
        await qaContext.SaveChangesAsync();

        //operator overload edge case tests
        //compare null with null (both should be equal to each other)
        (category1 <= null).Should().BeTrue();
        (category1 >= null).Should().BeTrue();
        (category1 == null).Should().BeTrue();
        (null <= category1).Should().BeTrue();
        (null >= category1).Should().BeTrue();
        (null == category1).Should().BeTrue();

        (category1 != null).Should().BeFalse();
        (category1 > null).Should().BeFalse();
        (category1 < null).Should().BeFalse();
        (null != category1).Should().BeFalse();
        (null > category1).Should().BeFalse();
        (null < category1).Should().BeFalse();

        //compare non-null with null (non-null should be greater or not equal)
        (category3 >= null).Should().BeTrue();
        (category3 > null).Should().BeTrue();
        (category3 != null).Should().BeTrue();
        (null != category3).Should().BeTrue();
        (null < category3).Should().BeTrue();
        (null <= category3).Should().BeTrue();

        (category3 <= null).Should().BeFalse();
        (category3 == null).Should().BeFalse();
        (category3 < null).Should().BeFalse();
        (null >= category3).Should().BeFalse();
        (null == category3).Should().BeFalse();
        (null > category3).Should().BeFalse();
    }
}