using System.Runtime.CompilerServices;
using AwesomeAssertions;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Logging;
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
        
        int size0 = Unsafe.SizeOf<DateTimeOffset>();
        int size1 = Unsafe.SizeOf<SourceKnownId>();
        int size2 = Unsafe.SizeOf<SourceKnownEntityId>();
        int size3 = Unsafe.SizeOf<TimeSpan>();
        int size4 = Unsafe.SizeOf<string>();
        int size5 = Unsafe.SizeOf<ScopeDuration>();
    }
}