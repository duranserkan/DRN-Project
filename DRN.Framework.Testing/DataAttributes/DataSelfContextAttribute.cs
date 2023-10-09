namespace DRN.Framework.Testing.DataAttributes;

/// <summary>
/// Provides <see cref="TestContext"/> as first parameter rest of the behaviour is same with DataInlineAuto <see cref="DataSelfAutoAttribute"/>
/// </summary>
public abstract class DataSelfContextAttribute : DataAttribute
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
                $"{GetType().FullName} must contain test data to be used as ClassData for the test method named '{testMethod.Name}' on {testMethod.DeclaringType?.FullName??string.Empty}");

        return _data.SelectMany(values => new DataInlineContextAttribute(values).GetData(testMethod)).ToArray();
    }
}