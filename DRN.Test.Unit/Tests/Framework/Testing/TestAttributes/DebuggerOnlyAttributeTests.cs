using System.Diagnostics;
using DRN.Framework.Testing.TestAttributes;

namespace DRN.Test.Unit.Tests.Framework.Testing.TestAttributes;

#pragma warning disable xUnit1006
public class DebuggerOnlyAttributeTests
{
    [Fact]
    public void DebuggerOnly_Attributes_Should_Skip_Unless_Debugger_Is_Attached()
    {
        var expectedSkip = Debugger.IsAttached ? null : "Only running in interactive mode.";
        new FactDebuggerOnlyAttribute().Skip.Should().Be(expectedSkip);
        new TheoryDebuggerOnlyAttribute().Skip.Should().Be(expectedSkip);
    }

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
