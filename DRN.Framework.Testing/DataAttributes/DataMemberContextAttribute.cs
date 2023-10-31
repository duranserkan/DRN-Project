using DRN.Framework.Testing.Contexts;

namespace DRN.Framework.Testing.DataAttributes;

/// <summary>
/// Provides a data source, inlines <see cref="TestContext"/> as first parameter rest of the behaviour is same with DataInlineAuto <see cref="DataMemberAutoAttribute"/>
/// </summary>
public  class DataMemberContextAttribute : MemberDataAttributeBase
{
    public DataMemberContextAttribute(string methodName, params object[] methodParams) : base(methodName, methodParams)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        var data = base.GetData(testMethod).SelectMany(values => new DataInlineContextAttribute(values)
            .GetData(testMethod)).ToArray();

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