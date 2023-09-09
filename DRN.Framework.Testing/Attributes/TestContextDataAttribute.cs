namespace DRN.Framework.Testing.Attributes;

public sealed class TestContextDataAttribute : CompositeDataAttribute
{
    public TestContextDataAttribute(params object[] data)
        : base(new InlineDataAttribute(new[] { new TestContext() }.Union(data).ToArray()), new NSubstituteAutoDataAttribute())
    {
    }
}