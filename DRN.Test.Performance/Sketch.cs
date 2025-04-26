using FluentAssertions;
using Flurl;

namespace DRN.Test.Performance;

public class Sketch
{
    [Fact]
    public void Doodle()
    {
        var u = "http://nexus/status/".AppendQueryParam("name", null)!;
        var x = u.ToString();
        x.Should().NotBeNull();
    }
}