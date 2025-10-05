using System.Security.Cryptography;
using DRN.Framework.Utils.Data.Encodings;
using Flurl;

namespace DRN.Test.Unit;

public class Sketch
{
    [Fact]
    public void Doodle()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var randomBytesBase64Url=randomBytes.Encode();
        
        var u = "http://nexus/status/".AppendQueryParam("name", null)!;
        var x = u.ToString();
        x.Should().NotBeNull();

        var stringTypeHashCode = typeof(string).GetHashCode();
        var intTypeHashCode = typeof(int).GetHashCode();
        
        stringTypeHashCode.Should().NotBe(intTypeHashCode);
    }
}