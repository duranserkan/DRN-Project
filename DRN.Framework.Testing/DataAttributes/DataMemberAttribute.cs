using DRN.Framework.Testing.Contexts;

namespace DRN.Framework.Testing.DataAttributes;

/// <summary>
/// Provides a data source same approach with <see cref="MemberDataAttribute"/> and generates missing data using AutoFixture and NSubstitute.
/// Also, if <see cref="DrnTestContext"/> is added as first parameter it automatically creates an instance and provides
///<b>This attribute can provide Complex Types that can not be provided by DataInline attributes</b>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class DataMemberAttribute(string methodName, params object[] methodParams) : MemberDataAttributeBase(methodName, methodParams)
{
    /// <inheritdoc />
    public override async ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker)
    {
        var drnTestContextData = await base.GetData(testMethod, disposalTracker);
        
        var resultData = new List<ITheoryDataRow>();
        foreach (var row in drnTestContextData)
        {
            var rowData = row.GetData();
            
            var attributeRows = await new DataInlineAttribute(rowData).GetData(testMethod, disposalTracker);
            resultData.AddRange(attributeRows);
        }

        return resultData;
    }
}