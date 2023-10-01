namespace DRN.Framework.Testing.DataAttributes;

/// <summary>
/// Provides a data source same approach with <see cref="MemberDataAttribute"/> and generates missing data using AutoFixture and NSubstitute.
///<b>This attribute can provide Complex Types that can not be provided by DataInline attributes</b>
/// </summary>
public class DataMemberAutoAttribute : MemberDataAttributeBase
{
    public DataMemberAutoAttribute(string methodName, params object[] methodParams) : base(methodName, methodParams)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        var data = base.GetData(testMethod)
            .SelectMany(values => new DataInlineAutoAttribute(values).GetData(testMethod)).ToArray();

        return data;
    }


    /// <inheritdoc />
    protected override object[]? ConvertDataItem(MethodInfo testMethod, object? item)
    {
        //From MemberDataAttribute.ConvertDataItem
        if (item == null) return null;
        if (item is not object[] array)
            throw new ArgumentException(
                $"Property {MemberName} on {MemberType ?? testMethod.DeclaringType} yielded an item that is not an object[]");
        return array;
    }
}