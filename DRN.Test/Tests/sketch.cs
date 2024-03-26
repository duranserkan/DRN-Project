using Flurl;

namespace DRN.Test.Tests;

public class sketch
{
    [Theory]
    [DataInline]
    public async Task Doodle(TestContext context)
    {
        var u = "http://nexus/status/".AppendQueryParam("name", null)!;
        var x=u.ToString();
    }
}