namespace DRN.Framework.Testing.Attributes;

public class NSubstituteInlineAutoDataAttribute : CompositeDataAttribute
{
    public NSubstituteInlineAutoDataAttribute(params object[] data) : base(new InlineDataAttribute(data), new NSubstituteAutoDataAttribute())
    {
    }
}