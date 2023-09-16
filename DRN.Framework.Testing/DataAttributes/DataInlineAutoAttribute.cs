namespace DRN.Framework.Testing.DataAttributes;

/// <summary>
/// Inlines data provided and generates missing data using AutoFixture and NSubstitute.
/// Have same constraints with <see cref="InlineDataAttribute"/>. Inlined data must be compile time constant expression
/// <b>To provide complex types use DataMember or DataSelf attributes</b>
/// </summary>
public class DataInlineAutoAttribute : CompositeDataAttribute
{
    public DataInlineAutoAttribute(params object[] data)
        : base(new InlineDataAttribute(data), new DataNSubstituteAutoAttribute())
    {
    }
}