using static DRN.Test.Analyzer.Tests.ClassificationTestFixture;

namespace DRN.Test.Analyzer.Tests;

public sealed class LoggingCaptureAnalysisTests
{
    [Theory]
    [DataInline("null")]
    [DataInline("\"\"")]
    [DataInline("\"[redacted]\"")]
    public async Task Safe_Constants_Should_Not_Waive_User_Defined_Logging_Conversions(string literal)
    {
        var diagnostics = await Analyze(Compilation($$"""
            using Microsoft.Extensions.Logging;
            using System.Collections.Generic;
            public sealed class ConvertedValue
            {
                public static implicit operator ConvertedValue(string value) => new();
                public static explicit operator string(ConvertedValue value) => "private";
            }
            public class Caller
            {
                public void Run(ILogger log)
                {
                    log.BeginScope(new Dictionary<string, object> { ["Password"] = (string)(ConvertedValue){{literal}} });
                    log.LogInformation("{Password}", (string)(ConvertedValue){{literal}});
                    log.BeginScope(new Dictionary<string, object> { ["Password"] = (object)(string){{literal}} });
                    log.LogInformation("{Password}", (object)(string){{literal}});
                }
            }
            """));

        diagnostics.Should().HaveCount(2).And.OnlyContain(item => item.Id == "DRNCLASSIFICATION008");
        diagnostics.Select(item => item.Location.SourceTree!.GetText().ToString(item.Location.SourceSpan))
            .Should().BeEquivalentTo(
                "log.BeginScope(new Dictionary<string, object> { [\"Password\"] = (string)(ConvertedValue)" + literal + " })",
                "log.LogInformation(\"{Password}\", (string)(ConvertedValue)" + literal + ")");
    }

    [Fact]
    public async Task Scope_Dictionaries_Should_Accept_Safe_Constants_But_Keep_Unsafe_Writes_Checked()
    {
        var diagnostics = await Analyze(Compilation("""
            using Microsoft.Extensions.Logging;
            using System.Collections.Generic;
            using DRN.Framework.SharedKernel.Attributes;
            public class Input { [Sensitive] public string Value => "private"; }
            public class Caller
            {
                public void Run(ILogger log, string value, Input input)
                {
                    log.BeginScope(new Dictionary<string, object>
                    {
                        ["Password"] = "[redacted]", ["Token"] = "", ["Email"] = null
                    });
                    log.BeginScope(new Dictionary<string, object> { { "Password", "[redacted]" } });
                    var safe = new Dictionary<string, object>();
                    safe["Password"] = "[redacted]";
                    safe.Add("Token", "");
                    safe["Email"] = null;
                    log.BeginScope(safe);
                    log.BeginScope(new Dictionary<string, object> { ["Payload"] = "[redacted]" });
                    log.BeginScope(new Dictionary<string, object> { ["Password"] = value });
                    log.BeginScope(new Dictionary<string, object> { ["Payload"] = value });
                    log.BeginScope(new Dictionary<string, object> { ["Password"] = "[redacted]suffix" });
                    log.BeginScope(new Dictionary<string, object> { ["Detail"] = input.Value });
                    var changing = new Dictionary<string, object> { ["Password"] = "[redacted]" };
                    changing["Password"] = value;
                    log.BeginScope(changing);
                }
            }
            """), new ProjectDiagnosticRulesTests.RuleFile("data-classification-rules.json",
            """{"sensitiveFieldPaths":["Payload"]}"""));

        diagnostics.Should().HaveCount(5).And.OnlyContain(item => item.Id == "DRNCLASSIFICATION008");
        diagnostics.Select(item => item.Location.SourceTree!.GetText().ToString(item.Location.SourceSpan))
            .Should().BeEquivalentTo(
                "log.BeginScope(new Dictionary<string, object> { [\"Password\"] = value })",
                "log.BeginScope(new Dictionary<string, object> { [\"Payload\"] = value })",
                "log.BeginScope(new Dictionary<string, object> { [\"Password\"] = \"[redacted]suffix\" })",
                "log.BeginScope(new Dictionary<string, object> { [\"Detail\"] = input.Value })",
                "log.BeginScope(changing)");
    }

    [Theory]
    [DataInline("LogTrace")]
    [DataInline("LogDebug")]
    [DataInline("LogInformation")]
    [DataInline("LogWarning")]
    [DataInline("LogError")]
    [DataInline("LogCritical")]
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
        diagnostics.Should().HaveCount(3).And.OnlyContain(item => item.Id == "DRNCLASSIFICATION008");
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
        diagnostics.Should().HaveCount(4).And.OnlyContain(item => item.Id == "DRNCLASSIFICATION008");
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
        diagnostics.Should().HaveCount(2).And.OnlyContain(item => item.Id == "DRNCLASSIFICATION008");
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
        diagnostics.Should().HaveCount(5).And.OnlyContain(item => item.Id == "DRNCLASSIFICATION008");
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

        diagnostics.Should().HaveCount(3).And.OnlyContain(item => item.Id == "DRNCLASSIFICATION008");
        diagnostics.Select(item => item.Location.SourceTree!.GetText().ToString(item.Location.SourceSpan))
            .Should().BeEquivalentTo("scoped.Add(\"Detail\", input.Value)",
                "log.LogInformation(\"{Detail}\", input.Value)",
                "log.BeginScope(new Dictionary<string, object> { [\"Detail\"] = input.Value })");
    }

    [Theory]
    [DataInline(255, false, false, null)]
    [DataInline(256, false, false, null)]
    [DataInline(257, false, false, "DRNCLASSIFICATION012")]
    [DataInline(255, true, false, "DRNCLASSIFICATION008")]
    [DataInline(256, true, false, "DRNCLASSIFICATION008")]
    [DataInline(257, true, false, "DRNCLASSIFICATION012")]
    [DataInline(257, true, true, "DRNCLASSIFICATION008")]
    public async Task Interface_Scan_Should_Distinguish_Completed_And_Incomplete_Captures(
        int propertyCount, bool classified, bool valueFirst, string? expectedId)
    {
        var members = string.Join("\n", Enumerable.Range(0, propertyCount - 1)
            .Select(index => $"string P{index} {{ get; }}"));
        var implementations = string.Join("\n", Enumerable.Range(0, propertyCount - 1)
            .Select(index => $"public string P{index} => \"ordinary\";"));
        var value = (classified ? "[Personal] " : "") + "string Value { get; }";
        var source = $$"""
            using DRN.Framework.SharedKernel.Attributes;
            using Microsoft.Extensions.Logging;
            using System.Collections.Generic;
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog { void Add(string key, object value); }
            }
            public interface IInput
            {
                {{(valueFirst ? value : members)}}
                {{(valueFirst ? members : value)}}
            }
            public class Input : IInput
            {
                {{implementations}}
                public string Value => "private";
            }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog scoped, ILogger log, Input input)
                {
                    scoped.Add("First", input.Value);
                    scoped.Add("Second", input.Value);
                    log.LogInformation("{First} {Second}", input.Value, input.Value);
                    log.BeginScope(new Dictionary<string, object> { ["Detail"] = input.Value });
                }
            }
            """;
        var diagnostics = await Analyze(Compilation(source));

        if (expectedId == null)
            diagnostics.Should().BeEmpty();
        else
        {
            // Positive captures report each value. Incomplete scans report once per call.
            diagnostics.Should().HaveCount(expectedId == "DRNCLASSIFICATION008" ? 5 : 4)
                .And.OnlyContain(item => item.Id == expectedId);
            diagnostics.Select(item => item.Location.SourceTree!.GetText().ToString(item.Location.SourceSpan))
                .Distinct().Should().BeEquivalentTo(
                    "scoped.Add(\"First\", input.Value)",
                    "scoped.Add(\"Second\", input.Value)",
                    "log.LogInformation(\"{First} {Second}\", input.Value, input.Value)",
                    "log.BeginScope(new Dictionary<string, object> { [\"Detail\"] = input.Value })");
        }

        (await Analyze(Compilation(source, "Caller.g.cs"))).Should().BeEmpty();
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
        diagnostic.Id.Should().Be("DRNCLASSIFICATION008");
        (await diagnostic.Location.SourceTree!.GetTextAsync()).ToString(diagnostic.Location.SourceSpan)
            .Should().Be("log.LogInformation(\"{Password}\", value)");
    }

    [Fact]
    public async Task Same_Name_Logging_Shims_Should_Not_Be_Treated_As_Framework_Logging()
    {
        var diagnostics = await Analyze(Compilation("""
            using Microsoft.Extensions.Logging;
            using System.Collections.Generic;

            namespace Microsoft.Extensions.Logging
            {
                public interface ILogger { void BeginScope<T>(T state); }
                public static class LoggerExtensions
                {
                    public static void LogInformation(this ILogger logger, string message, params object[] args) { }
                }
            }

            public class Caller
            {
                public void Run(ILogger log, string value)
                {
                    log.LogInformation("{Password}", value);
                    log.BeginScope(new Dictionary<string, object> { ["Password"] = value });
                }
            }
            """));

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Same_Name_Verification_Shims_Should_Not_Exempt_Framework_Logging()
    {
        var diagnostics = await Analyze(Compilation("""
            using Microsoft.Extensions.Logging;

            namespace NSubstitute
            {
                public static class SubstituteExtensions
                {
                    public static ILogger Received(this ILogger logger) => logger;
                }
            }

            namespace NSubstitute.ReceivedExtensions
            {
                public static class ReceivedExtensions
                {
                    public static ILogger Received(this ILogger logger) => logger;
                }
            }

            public class Caller
            {
                public void Run(ILogger log, string value)
                {
                    NSubstitute.SubstituteExtensions.Received(log).LogInformation("{Password}", value);
                    NSubstitute.ReceivedExtensions.ReceivedExtensions.Received(log).LogInformation("{Password}", value);
                }
            }
            """));

        diagnostics.Should().HaveCount(2).And.OnlyContain(item => item.Id == "DRNCLASSIFICATION008");
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
        ordinary.Should().ContainSingle().Which.Id.Should().Be("DRNCLASSIFICATION008");
        var generated = await Analyze(Compilation(source, "Caller.g.cs"));
        generated.Should().BeEmpty();
    }
}
