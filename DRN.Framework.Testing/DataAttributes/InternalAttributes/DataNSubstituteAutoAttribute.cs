namespace DRN.Framework.Testing.DataAttributes.InternalAttributes;

internal class DataNSubstituteAutoAttribute() : AutoDataAttribute(FixtureFactory)
{
    private static Func<IFixture> FixtureFactory => () => new Fixture().Customize(new AutoNSubstituteCustomization());
}