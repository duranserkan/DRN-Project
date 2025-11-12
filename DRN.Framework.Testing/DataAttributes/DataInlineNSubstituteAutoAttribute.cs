namespace DRN.Framework.Testing.DataAttributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal class DataInlineNSubstituteAutoAttribute(params object?[] objects) : InlineAutoDataAttribute(AutoNSubstituteFixtureFactory, objects)
{
    private static Func<IFixture> AutoNSubstituteFixtureFactory => () => new Fixture().Customize(new AutoNSubstituteCustomization());
}