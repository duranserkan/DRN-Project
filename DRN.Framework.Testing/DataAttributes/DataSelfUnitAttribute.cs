using DRN.Framework.Testing.Contexts;

namespace DRN.Framework.Testing.DataAttributes;

/// <summary>
/// Provides self-contained data from attributes derived from this attribute and generates missing data using AutoFixture and NSubstitute.
/// Also, if <see cref="DrnTestContextUnit"/> is added as first parameter it automatically creates an instance and provides
///<b>This attribute can provide Complex Types that can not be provided by DataInline attributes</b>
/// </summary>
public abstract class DataSelfUnitAttribute : DataAttribute
{
    private readonly List<object[]> _data = new(10);

    /// <summary>
    /// Adds a row to the theory.
    /// </summary>
    /// <param name="data">The values to be added.</param>
    protected void AddRow(params object[] data) => _data.Add(data);

    public override async ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker)
    {
        if (_data.Count == 0)
            throw new ArgumentException(
                $"{GetType().FullName} must contain test data to be used as ClassData for the test method named '{testMethod.Name}' on " +
                $"{testMethod.DeclaringType?.FullName ?? string.Empty}");
        
        var resultData = new List<ITheoryDataRow>();
        foreach (var values in _data)
        {
            var rowData = await new DataInlineUnitAttribute(values).GetData(testMethod, disposalTracker);
            resultData.AddRange(rowData);
        }

        return resultData;
    }
}