namespace DRN.Framework.Testing.Attributes;

public class NSubstituteDataAttribute : AutoDataAttribute
{
    private static Func<IFixture> FixtureFactory => () => new Fixture().Customize(new AutoNSubstituteCustomization());

    public NSubstituteDataAttribute() : base(FixtureFactory)
    {
    }
}
