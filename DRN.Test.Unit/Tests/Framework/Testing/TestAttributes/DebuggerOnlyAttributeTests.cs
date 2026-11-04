using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using DRN.Framework.Testing.TestAttributes;

namespace DRN.Test.Unit.Tests.Framework.Testing.TestAttributes;

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
    [SuppressMessage("Usage", "xUnit1006", Justification = "Parameterless theory intentionally verifies debugger-only execution without data dependencies.")]
    public void DebuggerOnlyTheory()
    {
    }
}
