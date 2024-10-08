using System.Diagnostics;

namespace DRN.Framework.Testing.TestAttributes;

//inspired from https://lostechies.com/jimmybogard/2013/06/20/run-tests-explicitly-in-xunit-net/
[XunitTestCaseDiscoverer("Xunit.Sdk.TheoryDiscoverer", "xunit.execution.{Platform}")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class TheoryDebuggerOnlyAttribute : TheoryAttribute
{
    public TheoryDebuggerOnlyAttribute()
    {
        if (!Debugger.IsAttached) Skip = "Only running in interactive mode.";
    }
}