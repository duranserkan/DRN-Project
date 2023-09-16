namespace DRN.Framework.Testing.DataAttributes;

internal class DataNSubstituteAutoAttribute : AutoDataAttribute
{
    private static Func<IFixture> FixtureFactory => () => new Fixture().Customize(new AutoNSubstituteCustomization());

    public DataNSubstituteAutoAttribute() : base(FixtureFactory)
    {
    }
}