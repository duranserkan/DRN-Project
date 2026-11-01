using System.Reflection;
using System.Text;
using System.Text.Json;
using DRN.Framework.SharedKernel.Json;
using DRN.Framework.Hosting.Middlewares;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;
using DRN.Framework.Utils.Logging.Diagnostics;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using DRN.Test.Unit.Tests.Framework.Utils.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Middlewares.ExceptionHandler;

public class DiagnosticModelProjectionTests
{
    [Theory]
    [DataInlineUnit(1, false)]
    [DataInlineUnit(2, false)]
    [DataInlineUnit(3, true)]
    [DataInlineUnit(4, true)]
    public void Header_Value_Limits_Should_Report_Only_Omitted_Values(int count, bool limited)
    {
        var projection = new DiagnosticModelProjection(new DiagnosticSanitizer(new DiagnosticLogOptions
        {
            MaxCollectionEntries = 2, MaxProcessingMilliseconds = 100
        }));
        var values = Enumerable.Range(1, count).Select(value => value.ToString()).ToArray();
        var headers = new HeaderDictionary { ["Accept"] = new Microsoft.Extensions.Primitives.StringValues(values) };

        var captured = projection.Headers(headers)["Accept"];

        captured.Should().Equal(limited ? [.. values.Take(2), DiagnosticSanitizer.Limited] : values);
    }

    [Theory]
    [DataInlineUnit(0, 0, 0, false, 0, false)]
    [DataInlineUnit(1, 0, 1, false, 0, false)]
    [DataInlineUnit(2, 0, 2, false, 0, false)]
    [DataInlineUnit(3, 0, 2, true, 0, false)]
    [DataInlineUnit(1, 1, 1, false, 1, false)]
    [DataInlineUnit(1, 2, 1, false, 1, true)]
    [DataInlineUnit(2, 1, 2, false, 0, true)]
    public void Frame_Limits_Should_Report_Only_Omitted_Frames(int firstCount, int secondCount,
        int firstCaptured, bool firstLimited, int secondCaptured, bool secondLimited)
    {
        var projection = new DiagnosticModelProjection(new DiagnosticSanitizer(new DiagnosticLogOptions
        {
            MaxCollectionEntries = 2, MaxProcessingMilliseconds = 100
        }));

        AssertFrames(firstCount, firstCaptured, firstLimited);
        AssertFrames(secondCount, secondCaptured, secondLimited);

        void AssertFrames(int count, int captured, bool limited)
        {
            var source = new ExceptionDetails("failure", Enumerable.Range(1, count)
                .Select(line => new StackFrameSourceCodeInfo { Line = line }));
            var frames = projection.Exception(source).StackFrames.ToArray();

            frames.Where(frame => frame.ErrorDetails != DiagnosticSanitizer.Limited)
                .Select(frame => frame.Line).Should().Equal(Enumerable.Range(1, captured));
            frames.Count(frame => frame.ErrorDetails == DiagnosticSanitizer.Limited).Should().Be(limited ? 1 : 0);
            if (limited) frames[^1].ErrorDetails.Should().Be(DiagnosticSanitizer.Limited);
        }
    }

    [Theory]
    [DataInlineUnit("Development", "page-probe")]
    [DataInlineUnit("Production", DiagnosticSanitizer.Redacted)]
    public async Task Page_Logs_And_Request_Details_Should_Opt_Out_Only_In_Development(string environment, string expected)
    {
        // Error handling must work after ordinary serialization has frozen the shared options.
        JsonSerializer.Serialize("warm-up", JsonConventions.DefaultOptions);
        JsonConventions.DefaultOptions.IsReadOnly.Should().BeTrue();
        using var settings = DiagnosticTestSupport.Settings(environment);
        var log = new ScopedLog(settings, new DiagnosticSanitizer(new DiagnosticLogOptions
        {
            // Exercise URL protection without classifying the entire route-value collection by its path.
            SensitivePathSegments = ["handoff"], MaxProcessingMilliseconds = 100
        }));
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
        context.Request.Path = "/handoff/path-probe";
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
