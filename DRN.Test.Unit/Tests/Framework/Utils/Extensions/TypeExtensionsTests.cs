using DRN.Framework.Utils.Extensions;

namespace DRN.Test.Unit.Tests.Framework.Utils.Extensions;

public class TypeExtensionsTests
{
    [Fact]
    public void GetPropertiesOfType_Should_Match_Exact_Type_And_BindingFlags()
    {
        var instance = typeof(Properties).GetPropertiesOfType<object>(BindingFlag.InstancePublic);
        instance.Select(property => property.Name).Should().Equal(nameof(Properties.Exact));

        var nonPublic = typeof(Properties).GetPropertiesOfType<object>(BindingFlag.InstanceNonPublic);
        nonPublic.Select(property => property.Name).Should().Equal("Hidden");
    }

    private sealed class Properties
    {
        public object Exact { get; } = new();
        public string Derived { get; } = "";
        public static object Static { get; } = new();
        private object Hidden { get; } = new();
    }
}
