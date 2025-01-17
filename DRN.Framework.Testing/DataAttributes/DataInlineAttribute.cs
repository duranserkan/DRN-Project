using DRN.Framework.Testing.Contexts;
using DRN.Framework.Testing.DataAttributes.InternalAttributes;

namespace DRN.Framework.Testing.DataAttributes;

/// <summary>
/// First Inlines data provided and then generates missing data using AutoFixture and NSubstitute.
/// Also, if test context is added as first parameter it automatically creates an instance and provides
/// Have same constraints with <see cref="InlineDataAttribute"/>. Inlined data must be compile-time constant expression
/// <b>To provide complex types use DataMember or DataSelf attributes</b>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class DataInlineAttribute : CompositeDataAttribute
{  //todo: verify inline and auto generated data
    public DataInlineAttribute(params object[] data) : base(new DataInlineContextAttribute(data), new DataNSubstituteAutoAttribute())
    {
    }

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        var dataRows = base.GetData(testMethod).ToArray(); 
        var hasTestContext = testMethod.GetParameters().FirstOrDefault()?.ParameterType == typeof(TestContext);
        if (hasTestContext)
            return dataRows.Select(row =>
            {
                ((TestContext)row[0]).MethodContext.SetTestData(row);
                return row;
            }).ToArray();

        return dataRows;
    }
}