namespace DRN.Test.Tests.Testing.TestAttributes;

public class DebuggerOnlyAttributeTests
{
    [FactDebuggerOnly]
    public void DebuggerOnlyFact()
    {
    }

    [TheoryDebuggerOnly]
    [DataInline]
    public void DebuggerOnlyTheory(TestContext context)
    {
    }
}