using FluentAssertions;
using Flurl;
using Xunit;

namespace DRN.Test.Unit.Tests;

public class Sketch
{
    [Fact]
    public void Doodle()
    {
        var u = "http://nexus/status/".AppendQueryParam("name", null)!;
        var x = u.ToString();
        x.Should().NotBeNull();

        var stringTypeHashCode = typeof(string).GetHashCode();
        var intTypeHashCode = typeof(int).GetHashCode();
        
        stringTypeHashCode.Should().NotBe(intTypeHashCode);
    }
}