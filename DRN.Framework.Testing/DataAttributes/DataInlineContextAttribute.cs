namespace DRN.Framework.Testing.DataAttributes;

/// <summary>
/// Inlines <see cref="TestContext"/> as first parameter rest of the behaviour is same with DataInlineAuto <see cref="DataInlineAutoAttribute"/>
/// </summary>
public sealed class DataInlineContextAttribute : DataInlineAutoAttribute
{
    public DataInlineContextAttribute(params object[] data) : base(new[] { new TestContext() }.Union(data).ToArray())
    {
    }

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        var dataRows = base.GetData(testMethod).ToArray();
        foreach (var row in dataRows)
            ((TestContext)row[0]).SetMethodInfo(testMethod);

        return dataRows;
    }
}