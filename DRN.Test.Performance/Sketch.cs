using FluentAssertions;
using Flurl;

namespace DRN.Test.Unit.Tests;

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