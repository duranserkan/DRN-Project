namespace DRN.Test.Tests.Framework.Testing.TestAttributes;

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