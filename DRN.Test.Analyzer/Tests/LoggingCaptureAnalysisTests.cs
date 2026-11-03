using static DRN.Test.Analyzer.Tests.DiagnosticClassificationAnalyzerTests;

namespace DRN.Test.Analyzer.Tests;

public sealed class LoggingCaptureAnalysisTests
{
    [Theory]
    [InlineData("LogTrace")]
    [InlineData("LogDebug")]
    [InlineData("LogInformation")]
    [InlineData("LogWarning")]
    [InlineData("LogError")]
    [InlineData("LogCritical")]
    public async Task Logger_Templates_Should_Check_Holes_And_Preserve_Escaped_Text(string method)
    {
        var diagnostics = await Analyze(Compilation($$$$"""
            using Microsoft.Extensions.Logging;
            public class Caller
            {
                public void Run(ILogger log, string value)
                {
                    log.{{{{method}}}}("Value {Password}", value);
                    log.{{{{method}}}}("Value {Password,12:X}", value);
                    log.{{{{method}}}}("Value {{{Password}}}", value);
                    log.{{{{method}}}}("Value {{Password}} {Status}", value);
                    log.{{{{method}}}}("Value {Password}", "[redacted]");
                    log.{{{{method}}}}("Value {Password}", "");
                    log.{{{{method}}}}("Value {Password}");
                    log.{{{{method}}}}("Value {Password}", (object[])null);
                    log.{{{{method}}}}("Value {Password}", (object)null);
                    log.{{{{method}}}}("Value {Status}", value);
                }
            }
            """));
        diagnostics.Should().HaveCount(3).And.OnlyContain(item => item.Id == "DRN0022");
        diagnostics.Select(item => item.Location.SourceTree!.GetText().ToString(item.Location.SourceSpan))
            .Should().BeEquivalentTo(
                $"log.{method}(\"Value {{Password}}\", value)",
                $"log.{method}(\"Value {{Password,12:X}}\", value)",
                "log." + method + "(\"Value {{{Password}}}\", value)");
    }

    [Fact]
    public async Task Logger_Overloads_Should_Use_Parameter_Names_And_Configured_Rules()
    {
        var diagnostics = await Analyze(Compilation("""
            using Microsoft.Extensions.Logging;
            public class Caller
            {
                public void Run(ILogger log, string value, System.Exception error)
                {
                    LoggerExtensions.LogInformation(log, message: "{Payload}", args: new object[] { value });
                    log.LogWarning(new EventId(1), error, "{Password}", value);
                    log.Log(LogLevel.Information, "{Password}", value);
                    log.BeginScope("{Password}", value);
                    log.LogInformation("{Payload}", "[redacted]");
                }
            }
            """), new ProjectDiagnosticRulesTests.RuleFile("data-classification-rules.json",
            """{"sensitiveFieldPaths":["Payload"]}"""));
        diagnostics.Should().HaveCount(4).And.OnlyContain(item => item.Id == "DRN0022");
    }

    [Fact]
    public async Task Scope_Dictionaries_Should_Check_Initializers_And_Prior_Writes()
    {
        var diagnostics = await Analyze(Compilation("""
            using Microsoft.Extensions.Logging;
            using System.Collections.Generic;
            public class Caller
            {
                public void Run(ILogger log, string value, string dynamicKey)
                {
                    log.BeginScope(new Dictionary<string, object> { ["Password"] = value });
                    var scope = new Dictionary<string, object>();
                    scope["Token"] = value;
                    log.BeginScope(scope);
                    log.BeginScope(new Dictionary<string, object> { ["Status"] = value });
                    log.BeginScope(new Dictionary<string, object> { [dynamicKey] = value });
                }
            }
            """));
        diagnostics.Should().HaveCount(2).And.OnlyContain(item => item.Id == "DRN0022");
        diagnostics.Select(item => item.Location.SourceTree!.GetText().ToString(item.Location.SourceSpan))
            .Should().BeEquivalentTo("log.BeginScope(new Dictionary<string, object> { [\"Password\"] = value })",
                "log.BeginScope(scope)");
    }

    [Fact]
    public async Task Classified_Properties_Should_Not_Lose_Protection_Under_Harmless_Keys()
    {
        var diagnostics = await Analyze(Compilation("""
            using DRN.Framework.SharedKernel.Attributes;
            using Microsoft.Extensions.Logging;
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog
                {
                    void Add(string key, object value);
                    void Add(string key, object value, string exemptionReason);
                    void AddIfNotNullOrEmpty(string key, string value);
                }
            }
            public interface IInput { [Personal] string Contact { get; } }
            public class Input : IInput
            {
                [Sensitive] public virtual string Value => "private";
                [Sensitive(false)] public string Status => "ok";
                public string Contact => "private";
            }
            public class Derived : Input { public override string Value => "private"; }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog scoped, ILogger log, Derived input)
                {
                    scoped.Add("Detail", input.Value);
                    scoped.Add("Detail", input.Value, "Reviewed name only.");
                    scoped.AddIfNotNullOrEmpty("Detail", input.Value);
                    scoped.Add("Detail", input.Contact);
                    log.LogInformation("{Detail}", input.Value);
                    scoped.Add("Detail", input.Status);
                    scoped.Add("Detail", "[redacted]");
                    log.LogInformation("{Detail}", input.Status);
                }
            }
            """));
        diagnostics.Should().HaveCount(5).And.OnlyContain(item => item.Id == "DRN0022");
    }

    [Fact]
    public async Task Inherited_Properties_Should_Keep_Receiver_Interface_Classifications()
    {
        var diagnostics = await Analyze(Compilation("""
            using DRN.Framework.SharedKernel.Attributes;
            using Microsoft.Extensions.Logging;
            using System.Collections.Generic;
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog { void Add(string key, object value); }
            }
            public interface IInput { [Personal] string Value { get; } }
            public class Base { public string Value => "private"; }
            public class Derived : Base, IInput { }
            public class Separate : Base, IInput { string IInput.Value => "private"; }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog scoped, ILogger log,
                    Derived input, Base ordinary, Separate separate)
                {
                    scoped.Add("Detail", input.Value);
                    log.LogInformation("{Detail}", input.Value);
                    log.BeginScope(new Dictionary<string, object> { ["Detail"] = input.Value });
                    scoped.Add("Detail", ordinary.Value);
                    log.LogInformation("{Detail}", separate.Value);
                    log.BeginScope(new Dictionary<string, object> { ["Detail"] = separate.Value });
                }
            }
            """));

        diagnostics.Should().HaveCount(3).And.OnlyContain(item => item.Id == "DRN0022");
        diagnostics.Select(item => item.Location.SourceTree!.GetText().ToString(item.Location.SourceSpan))
            .Should().BeEquivalentTo("scoped.Add(\"Detail\", input.Value)",
                "log.LogInformation(\"{Detail}\", input.Value)",
                "log.BeginScope(new Dictionary<string, object> { [\"Detail\"] = input.Value })");
    }

    [Fact]
    public async Task Lookalike_Logging_And_Substitute_Verifications_Should_Remain_Excluded()
    {
        var diagnostics = await Analyze(Compilation("""
            using Microsoft.Extensions.Logging;
            using NSubstitute;
            using System.Collections.Generic;
            public class OtherLogger
            {
                public void LogInformation(string message, params object[] args) { }
                public void BeginScope(object state) { }
            }
            public class Caller
            {
                public void Run(ILogger log, OtherLogger other, string value)
                {
                    other.LogInformation("{Password}", value);
                    other.BeginScope(new Dictionary<string, object> { ["Password"] = value });
                    log.Received().LogInformation("{Password}", value);
                    log.DidNotReceive().BeginScope(new Dictionary<string, object> { ["Password"] = value });
                    log.LogInformation("{Password}", value);
                }
            }
            """));
        var diagnostic = diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRN0022");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan)
            .Should().Be("log.LogInformation(\"{Password}\", value)");
    }

    [Fact]
    public async Task Dynamic_And_Generated_Logging_Should_Respect_Analysis_Boundaries()
    {
        const string source = """
            using Microsoft.Extensions.Logging;
            public class Caller
            {
                public void Run(ILogger log, string template, string value)
                {
                    log.LogInformation(template, value);
                    log.LogInformation("{Password}", value);
                }
            }
            """;
        var ordinary = await Analyze(Compilation(source));
        ordinary.Should().ContainSingle().Which.Id.Should().Be("DRN0022");
        var generated = await Analyze(Compilation(source, "Caller.g.cs"));
        generated.Should().BeEmpty();
    }
}
