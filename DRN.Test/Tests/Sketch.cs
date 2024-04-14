using Flurl;

namespace DRN.Test.Tests;

public class Sketch
{
    [Theory]
    [DataInline]
    public void Doodle(TestContext _)
    {
        var u = "http://nexus/status/".AppendQueryParam("name", null)!;
        var x=u.ToString();
    }
}