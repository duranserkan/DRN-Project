namespace DRN.Test.Unit.Tests.Framework.Testing.DataAttributes;

public class DataInlineAutoAttributeTests
{
    [Theory]
    [DataInlineUnit(10)]
    public void AutoInline_Should_Inline_And_Auto_Generate_Missing_Test_Data(int inline, Guid autoGenerate, IMockable mock)
    {
        inline.Should().Be(10);
        autoGenerate.Should().NotBeEmpty();
        mock.Max.Returns(65);
        mock.Max.Should().Be(65);
    }
}