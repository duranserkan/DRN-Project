namespace DRN.Framework.Testing.DataAttributes;

/// <summary>
/// Provides self contained data from attributes derived from this attribute and generates missing data using AutoFixture and NSubstitute.
///<b>This attribute can provide Complex Types that can not be provided by DataInline attributes</b>
/// </summary>
public abstract class DataSelfAutoAttribute : DataAttribute
{
    private readonly List<object[]> _data = new(10);

    /// <summary>
    /// Adds a row to the theory.
    /// </summary>
    /// <param name="data">The values to be added.</param>
    protected void AddRow(params object[] data)
    {
        _data.Add(data);
    }

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        if (!_data.Any())
            throw new ArgumentException(
                $"{GetType().FullName} must contain test data to be used as ClassData for the test method named '{testMethod.Name}' on {testMethod.DeclaringType.FullName}");

        return _data.SelectMany(values => new DataInlineAutoAttribute(values).GetData(testMethod));
    }
}