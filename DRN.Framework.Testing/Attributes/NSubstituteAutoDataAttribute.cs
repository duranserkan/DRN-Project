namespace DRN.Framework.Testing.Attributes;

public class NSubstituteAutoDataAttribute : AutoDataAttribute
{
    private static Func<IFixture> FixtureFactory => () => new Fixture().Customize(new AutoNSubstituteCustomization());

    public NSubstituteAutoDataAttribute() : base(FixtureFactory)
    {
    }
}
