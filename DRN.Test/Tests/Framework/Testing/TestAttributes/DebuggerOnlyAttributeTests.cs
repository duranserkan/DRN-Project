namespace DRN.Test.Tests.Framework.Testing.TestAttributes;

#pragma warning disable xUnit1006
public class DebuggerOnlyAttributeTests
{
    [FactDebuggerOnly]
    public void DebuggerOnlyFact()
    {
    }

    [TheoryDebuggerOnly]
    [DataInline]
    public void DebuggerOnlyTheory()
    {
    }
}
#pragma warning restore xUnit1006