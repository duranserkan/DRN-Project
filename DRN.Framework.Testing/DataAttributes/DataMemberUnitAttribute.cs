using DRN.Framework.Testing.Contexts;

namespace DRN.Framework.Testing.DataAttributes;

/// <summary>
/// Provides a data source same approach with <see cref="MemberDataAttribute"/> and generates missing data using AutoFixture and NSubstitute.
/// Also, if <see cref="UnitTestContext"/> is added as first parameter it automatically creates an instance and provides
///<b>This attribute can provide Complex Types that can not be provided by DataInline attributes</b>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class DataMemberUnitAttribute(string methodName, params object[] methodParams) : MemberDataAttributeBase(methodName, methodParams)
{
    /// <inheritdoc />
    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        var data = base.GetData(testMethod).SelectMany(values => new DataInlineUnitAttribute(values).GetData(testMethod)).ToArray();

        return data;
    }
    
    /// <inheritdoc />
    protected override object[]? ConvertDataItem(MethodInfo testMethod, object? item)
    {
        if (item == null) return null;
        if (item is not object[] array)
            throw new ArgumentException($"Property {MemberName} on {MemberType ?? testMethod.DeclaringType} yielded an item that is not an object[]");
        return array;
    }
}