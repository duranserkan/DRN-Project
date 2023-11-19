namespace DRN.Test.Tests.Testing.DataAttributes;

public class DataSelfContextAttributeTests
{
    [Theory]
    [DataSelfContextTestData1]
    public void TestContextClassData_Should_Inline_And_Auto_Generate_Missing_Test_Data(TestContext testContext,
        int inline, ComplexInline complexInline, Guid autoGenerate, IMockable mock)
    {
        testContext.Should().NotBeNull();
        testContext.MethodContext.TestMethod.Name.Should().Be(nameof(TestContextClassData_Should_Inline_And_Auto_Generate_Missing_Test_Data));
        inline.Should().BeGreaterThan(98);
        complexInline.Count.Should().BeLessThan(1001);
        autoGenerate.Should().NotBeEmpty();
        mock.Max.Returns(44);
        mock.Max.Should().Be(44);
    }
}

public class DataSelfContextTestData1 : DataSelfContextAttribute
{
    public DataSelfContextTestData1()
    {
        AddRow(99,new ComplexInline(100));
        AddRow(199,new ComplexInline(1000));
    }
}

public class TestContextClassDataTests2
{
    [Theory]
    [DataSelfContextTestData2]
    public void TestContextClassData_Should_Inline_And_Auto_Generate_Missing_Test_Data(TestContext testContext,
        int inline, ComplexInline complexInline, string autoGenerate, IMockable mock)
    {
        testContext.Should().NotBeNull();
        inline.Should().BeGreaterThan(1);
        complexInline.Count.Should().BeLessThan(4);
        autoGenerate.Should().NotBeEmpty();
        mock.Max.Returns(44);
        mock.Max.Should().Be(44);
    }
}

public class DataSelfContextTestData2 : DataSelfContextAttribute
{
    public DataSelfContextTestData2()
    {
        AddRow(2,new ComplexInline(2));
        AddRow(3,new ComplexInline(3));
    }
}