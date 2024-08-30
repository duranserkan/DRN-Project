namespace DRN.Framework.Testing.DataAttributes.InternalAttributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
internal class DataNSubstituteAutoAttribute() : AutoDataAttribute(FixtureFactory)
{
    private static Func<IFixture> FixtureFactory => () => new Fixture().Customize(new AutoNSubstituteCustomization());
}