using DRN.Framework.Testing;

namespace DRN.Test;

public class Test
{
    [Theory]
    [TestContextData(99)]
    public void x(TestContext context,int x, IDisposable disposable)
    {

    }
}
