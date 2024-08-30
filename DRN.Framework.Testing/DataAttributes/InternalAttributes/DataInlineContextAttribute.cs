using DRN.Framework.Testing.Contexts;

namespace DRN.Framework.Testing.DataAttributes.InternalAttributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
internal sealed class DataInlineContextAttribute : DataAttribute
{
    private readonly object[] _data;

    internal DataInlineContextAttribute(params object[] data) => _data = data;

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        var hasTestContext = testMethod.GetParameters().FirstOrDefault()?.ParameterType == typeof(TestContext);
        var row = hasTestContext ? new[] { new TestContext(testMethod) }.Union(_data).ToArray() : _data;

        return new[] { row };
    }
}