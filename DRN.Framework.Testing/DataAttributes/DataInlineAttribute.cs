using DRN.Framework.Testing.Contexts;

namespace DRN.Framework.Testing.DataAttributes;

/// <summary>
/// First Inlines data provided and then generates missing data using AutoFixture and NSubstitute.
/// Also, if <see cref="TestContextUnit"/> is added as first parameter it automatically creates an instance and provides
/// Have same constraints with <see cref="InlineDataAttribute"/>. Inlined data must be compile-time constant expression
/// <b>To provide complex types use DataMember or DataSelf attributes</b>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class DataInlineAttribute(params object[] data) : DataAttribute
{
    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        var hasTestContext = testMethod.GetParameters().FirstOrDefault()?.ParameterType == typeof(TestContext);
        if (hasTestContext)
        {
            var testContext = new TestContext(testMethod);
            var dataWithTestContext = new object[] { testContext }.Concat(data).ToArray();
            var testContextDataAttribute = new DataInlineNSubstituteAutoAttribute(dataWithTestContext);

            return testContextDataAttribute.GetData(testMethod).Select(row =>
            {
                ((TestContext)row[0]).MethodContext.SetTestData(row);
                return row;
            }).ToArray();
        }

        var dataAttribute = new DataInlineNSubstituteAutoAttribute(data);
        return dataAttribute.GetData(testMethod).ToArray();
    }
}