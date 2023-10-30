using DRN.Framework.Testing.TestAttributes;

namespace DRN.Test.Tests.Testing.TestAttributes;

public class DebuggerOnlyAttributeTests
{
    [FactDebuggerOnly]
    public void DebuggerOnlyFact()
    {

    }

    [TheoryDebuggerOnly]
    [DataInlineAuto]
    public void DebuggerOnlyTheory(int a)
    {

    }
}