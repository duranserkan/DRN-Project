namespace DRN.Test.Tests.Framework.Testing.DataAttributes;

public class DataMemberContextTests
{
    [Theory]
    [DataMember(nameof(TestContextInlineMemberData))]
    public void TestContextMember_Should_Inline_And_Auto_Generate_Missing_Test_Data(TestContext testContext,
        int inline, ComplexInline complexInline, Guid autoGenerate, IMockable mock)
    {
        testContext.Should().NotBeNull();
        testContext.MethodContext.TestMethod.Name.Should().Be(nameof(TestContextMember_Should_Inline_And_Auto_Generate_Missing_Test_Data));
        inline.Should().BeGreaterThan(10);
        complexInline.Count.Should().BeLessThan(10);
        autoGenerate.Should().NotBeEmpty();
        mock.Max.Returns(75);
        mock.Max.Should().Be(75);
    }

    public static IEnumerable<object[]> TestContextInlineMemberData => new List<object[]>
    {
        new object[] { 11, new ComplexInline(8) },
        new object[] { int.MaxValue, new ComplexInline(-1) }
    };
}