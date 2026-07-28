using DRN.Framework.Testing.Contexts;

namespace DRN.Framework.Testing.DataAttributes;

/// <summary>
/// First Inlines data provided and then generates missing data using AutoFixture and NSubstitute.
/// Also, if <see cref="DrnTestContextUnit"/> is added as first parameter it automatically creates an instance and provides
/// Have same constraints with <see cref="InlineDataAttribute"/>. Inlined data must be compile-time constant expression
/// <b>To provide complex types use DataMember or DataSelf attributes</b>
/// </summary>
/// <remarks>
/// This attribute's metadata takes precedence over metadata from the inner AutoFixture provider, while traits
/// are combined.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class DataInlineUnitAttribute(params object?[] data) : DataAttribute
{
    public override async ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker)
    {
        var hasDrnTestContext = testMethod.GetParameters().FirstOrDefault()?.ParameterType == typeof(DrnTestContextUnit);
        if (hasDrnTestContext)
        {
            var drnTestContextUnit = new DrnTestContextUnit(testMethod);
            var dataWithDrnTestContext = new object[] { drnTestContextUnit }.Concat(data).ToArray();
            var drnTestContextDataAttribute = new DataInlineNSubstituteAutoAttribute(dataWithDrnTestContext);

            var drnTestContextData = await drnTestContextDataAttribute.GetData(testMethod, disposalTracker);
            return drnTestContextData.Select(row =>
            {
                var rowData = row.GetData();

                ((DrnTestContextUnit)rowData[0]!).MethodContext.SetTestData(rowData!);
                return TheoryDataRowMetadata.ApplyAttributeToGeneratedRow(row, this);
            }).ToArray();
        }

        var dataAttribute = new DataInlineNSubstituteAutoAttribute(data);
        var dataCollection = await dataAttribute.GetData(testMethod, disposalTracker);
        return dataCollection
            .Select(row => TheoryDataRowMetadata.ApplyAttributeToGeneratedRow(row, this))
            .ToArray();
    }

    public override bool SupportsDiscoveryEnumeration() => true;
}
