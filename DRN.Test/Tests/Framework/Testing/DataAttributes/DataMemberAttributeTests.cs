namespace DRN.Test.Tests.Framework.Testing.DataAttributes;

public class DataMemberAttributeTests
{
    [Theory]
    [DataMember(nameof(DataMemberAutoData))]
    public void AutoMember_Should_Inline_And_Auto_Generate_Missing_Test_Data(int inline, ComplexInline complexInline, Guid autoGenerate, IMockable mock)
    {
        inline.Should().BeGreaterThan(10);
        complexInline.Count.Should().BeLessThan(10);
        autoGenerate.Should().NotBeEmpty();
        mock.Max.Returns(75);
        mock.Max.Should().Be(75);
    }

    public static IEnumerable<object[]> DataMemberAutoData => new List<object[]>
    {
        new object[] { 11, new ComplexInline(8) },
        new object[] { int.MaxValue, new ComplexInline(-1) }
    };
}