using DRN.Framework.Testing.Contexts;

namespace DRN.Framework.Testing.DataAttributes;

/// <summary>
/// First Inlines data provided and then generates missing data using AutoFixture and NSubstitute.
/// Also, if <see cref="DrnTestContext"/> is added as first parameter it automatically creates an instance and provides
/// Have same constraints with <see cref="InlineDataAttribute"/>. Inlined data must be compile-time constant expression
/// <b>To provide complex types use DataMember or DataSelf attributes</b>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class DataInlineAttribute(params object?[] data) : DataAttribute
{
    public override async ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker)
    {
        var hasTestContext = testMethod.GetParameters().FirstOrDefault()?.ParameterType == typeof(DrnTestContext);
        if (hasTestContext)
        {
            var testContext  = new DrnTestContext(testMethod);
            var dataWithTestContext = new object[] { testContext  }.Concat(data).ToArray();
            var testContextDataAttribute = new DataInlineNSubstituteAutoAttribute(dataWithTestContext);
            var testContextData = await testContextDataAttribute.GetData(testMethod, disposalTracker);

            return testContextData.Select(row =>
            {
                var rowData = row.GetData();

                ((DrnTestContext)rowData[0]!).MethodContext.SetTestData(rowData!);
                return row;
            }).ToArray();
        }

        var dataAttribute = new DataInlineNSubstituteAutoAttribute(data);
        var dataCollection = await dataAttribute.GetData(testMethod, disposalTracker);
        return dataCollection;
    }

    public override bool SupportsDiscoveryEnumeration() => true;
}