namespace DRN.Test.Tests.Framework.Testing.DataAttributes;

public class DataMemberContextTests
{
    [Theory]
    [DataMember(nameof(DrnTestContextInlineMemberData))]
    public void DrnTestContextMember_Should_Inline_And_Auto_Generate_Missing_Test_Data(DrnTestContext DrnTestContext,
        int inline, ComplexInline complexInline, Guid autoGenerate, IMockable mock)
    {
        DrnTestContext.Should().NotBeNull();
        DrnTestContext.MethodContext.TestMethod.Name.Should().Be(nameof(DrnTestContextMember_Should_Inline_And_Auto_Generate_Missing_Test_Data));
        inline.Should().BeGreaterThan(10);
        complexInline.Count.Should().BeLessThan(10);
        autoGenerate.Should().NotBeEmpty();
        mock.Max.Returns(75);
        mock.Max.Should().Be(75);
    }

    public static IEnumerable<object[]> DrnTestContextInlineMemberData => new List<object[]>
    {
        new object[] { 11, new ComplexInline(8) },
        new object[] { int.MaxValue, new ComplexInline(-1) }
    };
}