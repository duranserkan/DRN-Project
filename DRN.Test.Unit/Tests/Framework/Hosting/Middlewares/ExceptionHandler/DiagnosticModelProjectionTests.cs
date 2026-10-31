using System.Reflection;
using System.Text.Json;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;
using DRN.Framework.Utils.Logging.Diagnostics;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Middlewares.ExceptionHandler;

public class DiagnosticModelProjectionTests
{
    [Fact]
    public void Displayed_Exception_Copies_Should_Preserve_Text_And_Filter_References()
    {
        var original = new ReflectionTypeLoadException([], [new InvalidOperationException("password=loader-probe")], "token=message-probe");
        var source = new ExceptionDetails(original, [new StackFrameSourceCodeInfo
        {
            Function = "Service.Save", File = "Service.cs", Line = 42,
            ContextCode = ["var password = \"source-probe\";"]
        }]);
        var projection = new DiagnosticModelProjection(new DiagnosticSanitizer(new DiagnosticLogOptions()));
        var safe = projection.Exception(source);

        safe.Error.Should().BeSameAs(original);
        original.Message.Should().Contain("message-probe");
        var serialized = JsonSerializer.Serialize(safe);
        serialized.Should().Contain("message-probe").And.Contain("loader-probe").And.Contain("source-probe");
        serialized.Should().Contain("Service.Save").And.Contain("Service.cs");
        safe.StackFrames.Single().Line.Should().Be(42);
        safe.LoaderExceptionMessages.Should().ContainSingle().Which.Should().Be("password=loader-probe");
    }

    [Fact]
    public void Header_And_Cookie_Copies_Should_Protect_Values_Without_Mutating_Source()
    {
        var source = new Dictionary<string, string?[]> { ["Authorization"] = ["Basic header-probe"], ["Accept"] = ["application/json"] };
        var projection = new DiagnosticModelProjection(new DiagnosticSanitizer(new DiagnosticLogOptions()));
        var safe = projection.Strings(source);
        safe["Authorization"].Should().Equal(DiagnosticSanitizer.Redacted);
        safe["Accept"].Should().Equal("application/json");
        source["Authorization"].Should().Equal("Basic header-probe");
        projection.Strings(new Dictionary<string, string?[]> { ["CustomSession"] = ["cookie-probe"] }, true)
            ["CustomSession"].Should().Equal(DiagnosticSanitizer.Redacted);
    }
}
