namespace DRN.Test.Tests.Testing.TestAttributes;

public class DebuggerOnlyAttributeTests
{
    [FactDebuggerOnly]
    public void DebuggerOnlyFact()
    {
    }

    [TheoryDebuggerOnly]
    [DataInlineContext]
    public void DebuggerOnlyTheory(TestContext context)
    {
    }
}