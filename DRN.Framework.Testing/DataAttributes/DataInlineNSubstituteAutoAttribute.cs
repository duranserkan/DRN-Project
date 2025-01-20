namespace DRN.Framework.Testing.DataAttributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal class DataInlineNSubstituteAutoAttribute(params object[] objects) : InlineAutoDataAttribute(new DataNSubstituteAutoAttribute(), objects);

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal class DataNSubstituteAutoAttribute() : AutoDataAttribute(FixtureFactory)
{
    private static Func<IFixture> FixtureFactory => () => new Fixture().Customize(new AutoNSubstituteCustomization());
}