using System.Diagnostics;

namespace DRN.Framework.Testing.TestAttributes;

//inspired from https://lostechies.com/jimmybogard/2013/06/20/run-tests-explicitly-in-xunit-net/
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class FactDebuggerOnlyAttribute : FactAttribute
{
    public FactDebuggerOnlyAttribute()
    {
        if (!Debugger.IsAttached) Skip = "Only running in interactive mode.";
    }
}