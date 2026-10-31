using System.Reflection;
using System.Text;
using System.Text.Json;
using DRN.Framework.Hosting.Middlewares;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;
using DRN.Framework.Utils.Logging.Diagnostics;
using DRN.Framework.Utils.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Middlewares.ExceptionHandler;

public class DiagnosticModelProjectionTests
{
    [Theory]
    [DataInlineUnit("Development", "page-probe")]
    [DataInlineUnit("Production", DiagnosticSanitizer.Redacted)]
    public async Task Page_Logs_And_Request_Details_Should_Opt_Out_Only_In_Development(string environment, string expected)
    {
        using var settings = SettingsProvider.Development(new { Environment = environment });
        var log = new ScopedLog(settings, new DiagnosticSanitizer(new DiagnosticLogOptions { MaxProcessingMilliseconds = 100 }));
#pragma warning disable DRN0017 // Verify the page opt-out independently of normal log exports.
        log.Add("Password", "page-probe");
#pragma warning restore DRN0017
        using var services = new ServiceCollection().AddSingleton<IScopedLog>(log).BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        await using var body = new MemoryStream(Encoding.UTF8.GetBytes("{\"password\":\"body-probe\"}"));
        context.Request.Method = "POST";
        context.Request.Body = body;
        context.Request.ContentLength = body.Length;
        context.Request.ContentType = "application/json";
        context.Request.Path = "/resetpassword/path-probe";
        context.Request.QueryString = new QueryString("?token=query-probe");
        context.Request.Headers.Authorization = "Bearer header-probe";
        context.Request.Headers.Cookie = "session=cookie-probe";
        context.Request.RouteValues["token"] = "route-probe";
        RequestBufferingState.TryEnableBuffering(context, settings.Features);
        var host = Substitute.For<IWebHostEnvironment>();
        host.ContentRootFileProvider.Returns(new NullFileProvider());
        var options = Options.Create(new DeveloperExceptionPageOptions());
        var utils = new ExceptionUtils(new ExceptionDetailsProvider(options, host), settings, options);

        var model = await utils.CreateErrorPageModelAsync(context, new InvalidOperationException("failure"));

        model.Logs["Password"].Should().Be(expected);
        log.GetLogs()["Password"].Should().Be(DiagnosticSanitizer.Redacted);
        var development = environment == "Development";
        model.Headers["Authorization"].Single().Should().Be(development ? "Bearer header-probe" : DiagnosticSanitizer.Redacted);
        model.Cookies["session"].Should().Be(development ? "cookie-probe" : DiagnosticSanitizer.Redacted);
        model.RouteValues["token"].Should().Be(development ? "route-probe" : DiagnosticSanitizer.Redacted);
        model.RequestPath.Contains("path-probe", StringComparison.Ordinal).Should().Be(development);
        model.Query.QueryString!.Contains("query-probe", StringComparison.Ordinal).Should().Be(development);
        model.RequestBody.Contains("body-probe", StringComparison.Ordinal).Should().Be(development);
        if (development)
            JsonSerializer.Serialize(utils.CreateProblemDetails(context, new InvalidOperationException("failure")).Extensions)
                .Should().NotContain("header-probe").And.NotContain("cookie-probe").And.NotContain("route-probe");
    }

    [Fact]
    public void Display_Opt_Out_Should_Keep_Field_And_Collection_Limits()
    {
        using var settings = SettingsProvider.Development();
        var sanitizer = DiagnosticSanitizer.ForDevelopmentDisplay(settings, new DiagnosticLogOptions
        {
            MaxFieldBytes = 1024, MaxCollectionEntries = 2, MaxProcessingMilliseconds = 100
        });
        sanitizer.SanitizeBody(new string('x', 1025), "text/plain").Should().Be(DiagnosticSanitizer.Limited);
        sanitizer.SanitizeBody("token=form-probe", "application/x-www-form-urlencoded").Should().Contain("form-probe");
        sanitizer.SanitizeUrl("/?token=first&token=second&token=third").Should().Contain("first")
            .And.Contain("second").And.NotContain("third").And.Contain(DiagnosticSanitizer.Limited);
        var headers = new DiagnosticModelProjection(sanitizer).Strings(new Dictionary<string, string?[]>
        {
            ["Authorization"] = ["one", "two", "three"]
        });
        headers["Authorization"].Should().Equal("one", "two", DiagnosticSanitizer.Limited);
        Action useForLogging = () => new ScopedLog(settings, sanitizer);
        useForLogging.Should().Throw<ArgumentException>();
    }

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
