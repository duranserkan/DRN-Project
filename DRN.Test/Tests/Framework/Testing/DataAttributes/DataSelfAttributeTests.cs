namespace DRN.Test.Tests.Framework.Testing.DataAttributes;

public class DataSelfAttributeTests
{
    [Theory]
    [DataSelfTestData]
    public void AutoClass_Should_Inline_And_Auto_Generate_Missing_Test_Data(int inline, ComplexInline complexInline, Guid autoGenerate, IMockable mock)
    {
        inline.Should().BeGreaterThan(10);
        complexInline.Count.Should().BeLessThan(10);
        autoGenerate.Should().NotBeEmpty();
        mock.Max.Returns(75);
        mock.Max.Should().Be(75);
    }
}

public class DataSelfTestData : DataSelfAttribute
{
    public DataSelfTestData()
    {
        AddRow(200, new ComplexInline(9));
        AddRow(300, new ComplexInline(int.MinValue));
    }
}