using Sample.Domain.QA.Categories;

namespace DRN.Test.Unit.Tests.Sample.Domain;

public class CategoryEqualityTests
{
    [Fact]
    public void Category_Operators_Should_Order_Null_Before_NonNull()
    {
        Category? missing = null;
        var category = new Category("category");

        (missing <= null).Should().BeTrue();
        (missing >= null).Should().BeTrue();
        (missing == null).Should().BeTrue();
        (null <= missing).Should().BeTrue();
        (null >= missing).Should().BeTrue();
        (null == missing).Should().BeTrue();

        (missing != null).Should().BeFalse();
        (missing > null).Should().BeFalse();
        (missing < null).Should().BeFalse();
        (null != missing).Should().BeFalse();
        (null > missing).Should().BeFalse();
        (null < missing).Should().BeFalse();

        (category >= null).Should().BeTrue();
        (category > null).Should().BeTrue();
        (category != null).Should().BeTrue();
        (null != category).Should().BeTrue();
        (null < category).Should().BeTrue();
        (null <= category).Should().BeTrue();

        (category <= null).Should().BeFalse();
        (category == null).Should().BeFalse();
        (category < null).Should().BeFalse();
        (null >= category).Should().BeFalse();
        (null == category).Should().BeFalse();
        (null > category).Should().BeFalse();
    }
}
