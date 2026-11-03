using DRN.Framework.SharedKernel.Attributes;

namespace DRN.Test.Analyzer.Tests;

public sealed class DiagnosticClassificationAnalyzerTests
{
    [Fact]
    public void Public_Descriptors_Should_Preserve_Shared_Instances_And_Supported_Diagnostics()
    {
        var descriptors = new[]
        {
            (DiagnosticClassificationAnalyzer.MissingClassification, ClassificationDiagnosticDescriptors.MissingClassification, "DRN0015"),
            (DiagnosticClassificationAnalyzer.InvalidExemption, ClassificationDiagnosticDescriptors.InvalidExemption, "DRN0016"),
            (DiagnosticClassificationAnalyzer.PathClassification, ClassificationDiagnosticDescriptors.PathClassification, "DRN0017"),
            (DiagnosticClassificationAnalyzer.UnknownClassification, ClassificationDiagnosticDescriptors.UnknownClassification, "DRN0018"),
            (DiagnosticClassificationAnalyzer.AnonymousClassification, ClassificationDiagnosticDescriptors.AnonymousClassification, "DRN0019"),
            (DiagnosticClassificationAnalyzer.DictionaryClassification, ClassificationDiagnosticDescriptors.DictionaryClassification, "DRN0020"),
            (DiagnosticClassificationAnalyzer.InvalidRuleFile, ClassificationDiagnosticDescriptors.InvalidRuleFile, "DRN0021"),
            (DiagnosticClassificationAnalyzer.UnsafeLoggingCapture, ClassificationDiagnosticDescriptors.UnsafeLoggingCapture, "DRN0022"),
            (DiagnosticClassificationAnalyzer.InvalidNameExemptionComment, ClassificationDiagnosticDescriptors.InvalidNameExemptionComment, "DRN0023"),
            (DiagnosticClassificationAnalyzer.UnusedNameExemptionComment, ClassificationDiagnosticDescriptors.UnusedNameExemptionComment, "DRN0024")
        };

        foreach (var (publicDescriptor, sharedDescriptor, id) in descriptors)
        {
            publicDescriptor.Should().BeSameAs(sharedDescriptor);
            publicDescriptor.Id.Should().Be(id);
        }
        new DiagnosticClassificationAnalyzer().SupportedDiagnostics.Should().Equal(descriptors.Select(pair => pair.Item1));
    }

    [Fact]
    public async Task Classified_Roots_Should_Bypass_Matching_Paths_While_Scalars_Remain_Checked()
    {
        var diagnostics = await Analyze(Compilation("""
            using DRN.Framework.SharedKernel.Attributes;
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog
                {
                    void Add(string key, object value);
                    void AddProperties<T>(string prefix, T value) where T : class;
                }
            }
            [Sensitive] public struct Credential { }
            [Personal] public class Contact { }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log, Credential? credential, Contact contact)
                {
                    log.Add("Payload", credential);
                    log.Add("Payload", contact);
                    log.AddProperties("Payload", contact);
                    log.Add("Payload", 42);
                }
            }
            """), new ProjectDiagnosticRulesTests.RuleFile("data-classification-rules.json",
            """{"sensitiveFieldPaths":["Payload"]}"""));

        var diagnostic = diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRN0017");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan)
            .Should().Be("log.Add(\"Payload\", 42)");
    }

    [Fact]
    public async Task TimeSpan_Captures_Should_Check_Seconds_Paths_Only_For_Converted_Values()
    {
        var diagnostics = await Analyze(Compilation("""
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog
                {
                    void Add(string key, object value);
                    void AddToList(string key, object value);
                    void AddProperties<T>(string prefix, T value) where T : class;
                }
            }
            public class Model { public System.TimeSpan Duration => System.TimeSpan.Zero; }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log, System.TimeSpan? duration)
                {
                    log.Add("Duration", System.TimeSpan.Zero);
                    log.Add("Duration", duration);
                    log.AddToList("Duration", System.TimeSpan.Zero);
                    log.AddProperties("Metrics", new Model());
                    log.Add("Metrics", new Model());
                }
            }
            """), new ProjectDiagnosticRulesTests.RuleFile("data-classification-rules.json",
            """{"sensitiveFieldPaths":["Duration_Seconds"]}"""));

        diagnostics.Should().HaveCount(3).And.OnlyContain(item => item.Id == "DRN0017");
        diagnostics.Select(item => item.Location.SourceTree!.GetText().ToString(item.Location.SourceSpan))
            .Should().BeEquivalentTo("log.Add(\"Duration\", System.TimeSpan.Zero)",
                "log.Add(\"Duration\", duration)", "log.AddProperties(\"Metrics\", new Model())");
        diagnostics.Should().OnlyContain(item => item.GetMessage().Contains("Duration_Seconds", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Declaration_And_Capture_Should_Exclude_Ineligible_Properties()
    {
        var diagnostics = await Analyze(Compilation("""
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog { void Add(string key, object value); }
            }
            public class Model
            {
                public static string Password => "private";
                private string Secret => "private";
                public string Token { private get; set; } = "private";
                public string this[int index] => "private";
                public string Email => "private";
                public int Count => 1;
            }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log, Model model) => log.Add("Records", model);
            }
            """), new ProjectDiagnosticRulesTests.RuleFile("data-classification-rules.json",
            """
            {
                "secretFields":["Item"],
                "sensitiveFieldPaths":["Records.Password","Records.Secret","Records.Token","Records.Item","Records.Count"]
            }
            """));

        diagnostics.Should().HaveCount(2);
        var declaration = diagnostics.Should().ContainSingle(item => item.Id == "DRN0015").Which;
        declaration.GetMessage().Should().Contain("'Email'");
        var capture = diagnostics.Should().ContainSingle(item => item.Id == "DRN0017").Which;
        capture.GetMessage().Should().Contain("'Records.Count'");
        capture.Location.SourceTree!.GetText().ToString(capture.Location.SourceSpan).Should().Be("log.Add(\"Records\", model)");
    }

    [Theory]
    [InlineData("DRN.Framework.Utils.Logging", "IScopedLog", "Add", 2)]
    [InlineData("DRN.Framework.Utils.Logging", "ScopedLog", "Add", 2)]
    [InlineData("DRN.Framework.Utils.Logging.Diagnostics", "IDiagnosticSanitizer", "SanitizeValue", 2)]
    [InlineData("DRN.Framework.Utils.Logging.Diagnostics", "DiagnosticSanitizer", "SanitizeValue", 2)]
    [InlineData("Other.Logging", "ScopedLog", "Add", 0)]
    [InlineData("Other.Logging", "DiagnosticSanitizer", "SanitizeValue", 0)]
    public async Task Capture_Owners_Should_Check_Scalars_And_Dictionaries_Without_Matching_Lookalikes(
        string ownerNamespace, string owner, string method, int expectedCount)
    {
        var diagnostics = await Analyze(Compilation($$"""
            namespace {{ownerNamespace}}
            {
                public class {{owner}} { public void {{method}}(string key, object value) { } }
            }
            public class Caller
            {
                public void Run({{ownerNamespace}}.{{owner}} capture)
                {
                    capture.{{method}}("Password", "private");
                    capture.{{method}}("Payload", new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["Password"] = "private"
                    });
                }
            }
            """));
        diagnostics.Should().HaveCount(expectedCount);
        if (expectedCount == 0) return;
        diagnostics.Should().ContainSingle(item => item.Id == "DRN0017");
        diagnostics.Should().ContainSingle(item => item.Id == "DRN0020");
    }

    [Theory]
    [InlineData("DRN.Framework.Utils.Logging", "interface", "IScopedLog", true)]
    [InlineData("DRN.Framework.Utils.Logging", "class", "ScopedLog", true)]
    [InlineData("Other.Logging", "class", "ScopedLog", false)]
    public async Task Conditional_String_Captures_Should_Check_Constant_Keys_And_Ignore_Uncaptured_Values(
        string ownerNamespace, string declaration, string owner, bool supported)
    {
        var method = declaration == "interface"
            ? "void AddIfNotNullOrEmpty(string key, string value);"
            : "public void AddIfNotNullOrEmpty(string key, string value) { }";
        var diagnostics = await Analyze(Compilation($$"""
            namespace {{ownerNamespace}}
            {
                public {{declaration}} {{owner}} { {{method}} }
            }
            public class Caller
            {
                public void Run({{ownerNamespace}}.{{owner}} log, string value, string dynamicKey)
                {
                    const string SensitiveKey = "Password";
                    const string Redacted = "[redacted]";
                    log.AddIfNotNullOrEmpty("Password", value);
                    log.AddIfNotNullOrEmpty(value: value, key: SensitiveKey);
                    log.AddIfNotNullOrEmpty("Password", "[redacted]secret");
                    log.AddIfNotNullOrEmpty("Payload", value);
                    log.AddIfNotNullOrEmpty("Status", value);
                    log.AddIfNotNullOrEmpty(dynamicKey, value);
                    log.AddIfNotNullOrEmpty("Password", Redacted);
                    log.AddIfNotNullOrEmpty("Password", "");
                    log.AddIfNotNullOrEmpty("Password", null);
                    log.AddIfNotNullOrEmpty("Payload", "");
                }
            }
            """), new ProjectDiagnosticRulesTests.RuleFile("data-classification-rules.json",
            """{"sensitiveFieldPaths":["Payload"]}"""));

        if (!supported)
        {
            diagnostics.Should().BeEmpty();
            return;
        }
        diagnostics.Should().HaveCount(4).And.OnlyContain(item => item.Id == "DRN0017");
        diagnostics.Select(item => item.Location.SourceTree!.GetText().ToString(item.Location.SourceSpan))
            .Should().BeEquivalentTo(
                "log.AddIfNotNullOrEmpty(\"Password\", value)",
                "log.AddIfNotNullOrEmpty(value: value, key: SensitiveKey)",
                "log.AddIfNotNullOrEmpty(\"Password\", \"[redacted]secret\")",
                "log.AddIfNotNullOrEmpty(\"Payload\", value)");
    }

    [Fact]
    public async Task Redacted_Constants_Should_Not_Require_Capture_Exemptions()
    {
        var diagnostics = await Analyze(Compilation("""
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog { void Add(string key, object value); }
            }
            public class Caller
            {
                private const string Redacted = "[redacted]";
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log, string value)
                {
                    log.Add("Secret", Redacted);
                    log.Add("Password", "[redacted]");
                    log.Add("Password", value);
                    log.Add("Password", "[redacted]secret");
                    log.Add("Password", Redacted + value);
                }
            }
            """));
        diagnostics.Should().HaveCount(3).And.OnlyContain(item => item.Id == "DRN0017");
    }

    [Fact]
    public async Task Ip_Names_Should_Use_The_Shared_Baseline_Without_Duplicate_Diagnostics()
    {
        var compilation = Compilation("""
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog { void Add(string key, object value); }
            }
            public class Request { public string IpAddress { get; set; } }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log, string address)
                {
                    log.Add("Request_IpAddress", address);
                    log.Add("Remote_IpAddr", address);
                }
            }
            """);

        (await Analyze(compilation)).Should().HaveCount(3);
        var configured = await Analyze(compilation, new ProjectDiagnosticRulesTests.RuleFile("data-classification-rules.json",
            """{"personalFields":["ipadd"]}"""));
        configured.Should().HaveCount(3);
        configured.Should().ContainSingle(item => item.Id == "DRN0015");
        configured.Count(item => item.Id == "DRN0017").Should().Be(2);
    }

    [Fact]
    public async Task Migration_Column_Definitions_And_Constraint_Selectors_Should_Be_Exempt()
    {
        var diagnostics = await Analyze(Compilation("""
            using Microsoft.EntityFrameworkCore.Migrations;
            public class InitialMigration : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.CreateTable("users", table => new
                    {
                        address_postcode = table.Column<string>(nullable: true),
                        contact_email = table.Column<string>(nullable: true),
                        contact_phone = table.Column<string>(nullable: true),
                        password_hash = table.Column<string>(nullable: true),
                        provider_key = table.Column<string>(nullable: false).Annotation("Example", true)
                    }, constraints: table =>
                    {
                        table.PrimaryKey("pk_users", x => new { x.provider_key, x.contact_email });
                        table.UniqueConstraint("uq_users", x => new { x.contact_phone, x.contact_email });
                        table.ForeignKey("fk_users", x => new { x.provider_key, x.contact_email },
                            "accounts", new[] { "id", "email" });
                    });
                }
            }
            """));
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Migration_Exemption_Should_Not_Cover_Data_Logging_Or_Lookalike_Apis()
    {
        var diagnostics = await Analyze(Compilation("""
            using System;
            using Microsoft.EntityFrameworkCore.Migrations;
            using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog { void Add(string key, object value); }
            }
            public class InitialMigration : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    var data = new { Password = "private" };
                    migrationBuilder.InsertData("users", "payload", new { Email = "private" });
                    migrationBuilder.CreateTable("users", table => new
                    {
                        email = table.Column<string>(nullable: true, defaultValue: new { Password = "private" })
                    });
                    migrationBuilder.CreateTable("invalid", table => new { Password = "private" });
                    Helpers.CreateTable(table => new { Email = table.Column<string>(nullable: true) });
                }
                public void Log(DRN.Framework.Utils.Logging.IScopedLog log) => log.Add("Password", "private");
            }
            public static class Helpers
            {
                public static void CreateTable<T>(Func<ColumnsBuilder, T> columns) => throw new NotSupportedException();
            }
            """, "Migrations/InitialMigration.cs"));
        diagnostics.Count(item => item.Id == "DRN0019").Should().Be(5);
        diagnostics.Should().ContainSingle(item => item.Id == "DRN0017");
        diagnostics.Should().HaveCount(6);
    }

    [Fact]
    public async Task Substitute_Verifications_Should_Not_Be_Treated_As_Captures()
    {
        var compilation = Compilation("""
            using System.Collections.Generic;
            using NSubstitute;
            using NSubstitute.ReceivedExtensions;
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog
                {
                    void Add(string key, object value);
                    void AddIfNotNullOrEmpty(string key, string value);
                }
            }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log)
                {
                    log.Received().Add("PreAuthRateLimitRejectedIp", Arg.Any<object>());
                    log.Received(1).Add("PreAuthRateLimitRejected", true);
                    log.DidNotReceive().Add("PreAuthRateLimitRejectedPartition", "private");
                    log.ReceivedWithAnyArgs().Add("PreAuthRateLimitRejectedAction", "deny");
                    log.ReceivedWithAnyArgs(1).Add("PreAuthRateLimitRejectedAction", "deny");
                    log.DidNotReceiveWithAnyArgs().Add("PreAuthRateLimitRejected", false);
                    log.Received(Quantity.Exactly(1)).Add("PreAuthRateLimitRejected", true);
                    SubstituteExtensions.Received(log).Add("PreAuthRateLimitRejected", true);
                    log.Received().AddIfNotNullOrEmpty("Password", Arg.Any<string>());
                    log.DidNotReceive().AddIfNotNullOrEmpty("Password", Arg.Any<string>());
                    var values = new Dictionary<string, object> { ["Password"] = "private" };
                    log.Received().Add("Response", values);
                    log.DidNotReceive().Add("Response", values);
                    log.Add("PreAuthRateLimitRejected", true);
                    log.Add("Response", values);
                }
            }
            """);
        var diagnostics = await Analyze(compilation);
        diagnostics.Should().HaveCount(2);
        diagnostics.Should().ContainSingle(item => item.Id == "DRN0017");
        diagnostics.Should().ContainSingle(item => item.Id == "DRN0020");
        diagnostics.Should().OnlyContain(item => item.Location.SourceTree!.GetText().ToString(item.Location.SourceSpan).StartsWith("log.Add(", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Verification_Exemption_Should_Not_Suppress_Declarations_Or_Lookalike_Methods()
    {
        var diagnostics = await Analyze(Compilation("""
            using NSubstitute;
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog { void Add(string key, object value); }
            }
            public static class Helpers
            {
                public static T Received<T>(T value) => value;
            }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log)
                {
                    Helpers.Received(log).Add("Password", "private");
                    log.Received().Add("Response", new { Password = "private" });
                }
            }
            """));
        diagnostics.Should().HaveCount(2);
        diagnostics.Should().ContainSingle(item => item.Id == "DRN0017");
        diagnostics.Should().ContainSingle(item => item.Id == "DRN0019");
    }

    [Fact]
    public async Task Scalar_Dictionary_Projections_Should_Not_Classify_The_Receiver()
    {
        var compilation = Compilation("""
            using System.Collections.Generic;
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog { void Add(string key, object value); }
            }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log)
                {
                    var values = new Dictionary<string, object> { ["Password"] = "private" };
                    values["Email"] = "private";
                    var count = values.Count;
                    bool? present = values.ContainsKey("Password");
                    log.Add("Count", values.Count);
                    log.Add("Present", values.ContainsKey("Password"));
                    log.Add("Count", count);
                    log.Add("Present", present);
                    log.Add("Summary", new { Count = values.Count, Present = values.ContainsKey("Password") });
                    log.Add("Response", values);
                }
            }
            """);
        var diagnostics = await Analyze(compilation);
        diagnostics.Should().HaveCount(2).And.OnlyContain(item => item.Id == "DRN0020");
        diagnostics.Should().OnlyContain(item => item.Location.SourceTree!.GetText().ToString(item.Location.SourceSpan) ==
            "log.Add(\"Response\", values)");
    }

    [Fact]
    public async Task Dictionary_Captures_Should_Reject_Known_Keys_In_Initializers_And_Prior_Writes()
    {
        var diagnostics = await Analyze(Compilation("""
            using System.Collections.Generic;
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog { void Add(string key, object value); void AddWithPolicy(string key, object value, object policy); }
            }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log, string prefix)
                {
                    var values = new Dictionary<string, object> { { "Password", "private" } };
                    values["Email"] = "private";
                    values.Add("StatusCode", 200);
                    log.Add(prefix, values);
                    log.AddWithPolicy(prefix, values, new object());
                    log.Add(prefix, new Dictionary<string, object>
                    {
                        ["Detail"] = new Dictionary<string, object> { ["Token"] = "private" }
                    });
                }
            }
            """));
        diagnostics.Should().HaveCount(7).And.OnlyContain(item => item.Id == "DRN0020");
        diagnostics.Should().OnlyContain(item => item.GetMessage().Contains("AddDictionary", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Dictionary_Captures_Should_Inspect_Prior_Assignments_To_The_Captured_Local()
    {
        var diagnostics = await Analyze(Compilation("""
            using System.Collections.Generic;
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog { void Add(string key, object value); }
            }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log)
                {
                    var values = new Dictionary<string, object>();
                    var other = new Dictionary<string, object>();
                    other = new Dictionary<string, object> { ["Token"] = "private" };
                    log.Add("Before", values);
                    values = new Dictionary<string, object> { ["Password"] = "private" };
                    log.Add("Response", values);
                    values = new Dictionary<string, object> { ["Email"] = "private" };
                }
                public void AssignedAfterDeclaration(DRN.Framework.Utils.Logging.IScopedLog log)
                {
                    Dictionary<string, object> values;
                    values = new Dictionary<string, object>
                    {
                        ["Nested"] = new Dictionary<string, object> { ["StatusCode"] = 503 }
                    };
                    log.Add("NestedResponse", values);
                }
            }
            """));
        diagnostics.Should().HaveCount(2).And.OnlyContain(item => item.Id == "DRN0020");
        diagnostics.Should().ContainSingle(item => item.GetMessage().Contains("'Password'", StringComparison.Ordinal) &&
            item.Location.SourceTree!.GetText().ToString(item.Location.SourceSpan) == "log.Add(\"Response\", values)");
        diagnostics.Should().ContainSingle(item => item.GetMessage().Contains("'StatusCode'", StringComparison.Ordinal) &&
            item.Location.SourceTree!.GetText().ToString(item.Location.SourceSpan) == "log.Add(\"NestedResponse\", values)");
    }

    [Fact]
    public async Task Dictionary_Initializers_Should_Be_Tracked_In_Closures_By_Local_Symbol()
    {
        var diagnostics = await Analyze(Compilation("""
            using System;
            using System.Collections.Generic;
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog { void Add(string key, object value); }
            }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log)
                {
                    var values = new Dictionary<string, object> { ["Password"] = "private" };
                    Action capture = () => log.Add("Lambda", values);
                    void Capture() => log.Add("LocalFunction", values);
                }
                public void Other(DRN.Framework.Utils.Logging.IScopedLog log)
                {
                    var values = new Dictionary<string, object> { ["Count"] = 1 };
                    log.Add("Harmless", values);
                }
            }
            """));
        diagnostics.Should().HaveCount(2).And.OnlyContain(item => item.Id == "DRN0020");
        diagnostics.Should().ContainSingle(item => item.Location.SourceTree!.GetText().ToString(item.Location.SourceSpan) ==
            "log.Add(\"Lambda\", values)");
        diagnostics.Should().ContainSingle(item => item.Location.SourceTree!.GetText().ToString(item.Location.SourceSpan) ==
            "log.Add(\"LocalFunction\", values)");
    }

    [Fact]
    public async Task Dictionary_API_Should_Accept_Own_Keys_But_Reject_Nested_Matching_Keys()
    {
        var diagnostics = await Analyze(Compilation("""
            using System.Collections.Generic;
            using DRN.Framework.Utils.Logging.Diagnostics;
            namespace DRN.Framework.Utils.Logging { public interface IScopedLog { } }
            namespace DRN.Framework.Utils.Logging.Diagnostics
            {
                public static class DiagnosticCaptureExtensions
                {
                    public static void AddDictionary<T>(this DRN.Framework.Utils.Logging.IScopedLog log,
                        string key, IReadOnlyDictionary<string, T> values, params object[] classifications) { }
                }
            }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log)
                {
                    var values = new Dictionary<string, object> { ["StatusCode"] = 200 };
                    log.AddDictionary("Response", values);
                    log.AddDictionary("Response", new Dictionary<string, object>
                    {
                        ["Nested"] = new Dictionary<string, object> { ["Password"] = "private" }
                    });
                }
            }
            """));
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRN0020");
    }

    [Fact]
    public async Task Harmless_And_Dynamic_Dictionary_Keys_Should_Retain_Runtime_Protection()
    {
        var diagnostics = await Analyze(Compilation("""
            using System.Collections.Generic;
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog { void Add(string key, object value); }
            }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log, string key)
                {
                    var values = new Dictionary<string, object> { ["Count"] = 3, [key] = "private" };
                    log.Add("Response", values);
                    var unused = new Dictionary<string, object> { ["Password"] = "private" };
                }
            }
            """));
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Anonymous_Properties_Should_Require_Named_Models_Without_Logging()
    {
        const string source = """
            using DRN.Framework.SharedKernel.Attributes;
            public class Input { [Personal] public string Email => "private"; }
            public class Caller
            {
                public object Run(Input input, string Password)
                {
                    var first = new { StatusCode = 200, Password, input.Email, S_e_C_r_E_t = "private" };
                    var repeated = new { StatusCode = 200, Password, input.Email, S_e_C_r_E_t = "private" };
                    return new { first, repeated };
                }
            }
            """;
        var diagnostics = await Analyze(Compilation(source));
        diagnostics.Should().HaveCount(8).And.OnlyContain(item => item.Id == "DRN0019" && item.Severity == DiagnosticSeverity.Error);
        foreach (var name in new[] { "StatusCode", "Password", "Email", "S_e_C_r_E_t" })
            diagnostics.Count(item => item.GetMessage().Contains("Anonymous property '" + name + "'", StringComparison.Ordinal))
                .Should().Be(2);
        diagnostics.Should().OnlyContain(item => item.GetMessage().Contains("named class or record", StringComparison.Ordinal));
        diagnostics.Select(item => item.Location.SourceSpan).Distinct().Should().HaveCount(8);
    }

    [Fact]
    public async Task Nested_Anonymous_Properties_Should_Be_Checked_In_Collections_And_Lambdas()
    {
        var diagnostics = await Analyze(Compilation("""
            public class Caller
            {
                public object Run(string Password)
                {
                    System.Func<object> factory = () => new { Container = new { Email = "private" } };
                    return new
                    {
                        Result = new { Items = new[] { new { Password }, new { Password = "another" } } },
                        Factory = factory
                    };
                }
            }
            """));
        diagnostics.Should().HaveCount(3).And.OnlyContain(item => item.Id == "DRN0019");
    }

    [Fact]
    public async Task Capture_Exceptions_Should_Not_Suppress_Anonymous_Declaration_Requirements()
    {
        var diagnostics = await Analyze(Compilation("""
            using DRN.Framework.SharedKernel.Attributes;
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog
                {
                    void Add(string key, object value, string exemptionReason);
                    void AddWithPolicy(string key, object value, object policy);
                    void AddPropertiesWithPolicy<T>(string key, T value, object policy) where T : class;
                }
            }
            [IgnoreLog]
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log, string prefix, object policy)
                {
                    log.Add(prefix, new { StatusCode = 200 }, exemptionReason: "HTTP status.");
                    log.AddWithPolicy(prefix, new { Password = "private" }, policy);
                    log.AddPropertiesWithPolicy(prefix, new { Detail = new { Email = "private" } }, policy);
                }
            }
            """));
        diagnostics.Should().HaveCount(3).And.OnlyContain(item => item.Id == "DRN0019");
    }

    [Fact]
    public async Task Named_Annotated_Records_And_Harmless_Anonymous_Objects_Should_Remain_Allowed()
    {
        var diagnostics = await Analyze(Compilation("""
            using DRN.Framework.SharedKernel.Attributes;
            public record Response(
                [property: Sensitive(false, Justification = "HTTP status.")] int StatusCode,
                [property: Sensitive] string Password,
                [property: Personal] string Email);
            public class Caller
            {
                public object Run() => new
                {
                    Result = new Response(200, "private", "private@example.test"),
                    Items = new[] { new { Count = 3, Completed = true, Section = "overview" } },
                    Message = "password=private"
                };
            }
            """));
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Generated_Anonymous_Properties_Should_Not_Report_Diagnostics()
    {
        var diagnostics = await Analyze(Compilation("""
            // <auto-generated/>
            public class Generated { public object Value => new { Detail = new { Token = "private" } }; }
            """, "Models.g.cs"));
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Catalog_Normalization_Should_Match_Browser_Name_Vectors()
    {
        foreach (var name in new[]
                 {
                     "Response_StatusCode", "Secret", "S_e_C_r_E_t", "CancellationToken", "Keyboard",
                     "E_m_a_i_l", "ClientSecret", "Client_Assertion", "Client", "ClientId", "ClientErrorId", "Request_l5d_client_id",
                     "IpAddress", "Remote_IpAddr", "PreAuthReplenishmentSeconds", "PostAuthReplenishmentSeconds",
                     "ControllerClassName", "DiagnosticRedacted", "C_V_V", "Cvc", "Pin_Code",
                     "User_Name", "FirstName", "Last_Name", "DateOfBirth", "Birth_Date", "IBAN"
                 })
        {
            var source = "public class Model { public string " + name + " { get; set; } = \"\"; }";
            (await Analyze(Compilation(source))).Should().ContainSingle().Which.Id.Should().Be("DRN0015");
        }
        (await Analyze(Compilation("public class Model { public string Émail { get; set; } = \"\"; }"))).Should().BeEmpty();
        foreach (var name in new[]
                 {
                     "Sec", "ElapsedSeconds", "Section", "FlurlEx_ResponseCaptureLimited",
                     "DriftSeconds", "ReplenishmentSeconds", "MaxProcessingMilliseconds", "Secure",
                     "UseSecureSourceKnownIds", "SecondEntities", "DisposeCount", "SecureId",
                     "NullableSecureId", "FileName", "ServiceAddress", "ShippingMethod", "Panel", "RootPath"
                 })
            (await Analyze(Compilation("public class Model { public int " + name + " { get; set; } }"))).Should().BeEmpty();
    }

    [Fact]
    public async Task Nullable_Classified_Types_And_TimeSpan_Capture_Should_Follow_Runtime_Contract()
    {
        var diagnostics = await Analyze(Compilation("""
            using DRN.Framework.SharedKernel.Attributes;
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog
                {
                    void Add(string key, object value);
                    void AddProperties<T>(string prefix, T value) where T : class;
                }
            }
            [Sensitive] public struct Credential { }
            public class Model
            {
                public Credential? Password => throw new System.Exception();
                public System.TimeSpan Duration => System.TimeSpan.Zero;
            }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log)
                {
                    log.Add("Duration", System.TimeSpan.Zero);
                    log.AddProperties("Model", new Model());
                }
            }
            """));
        diagnostics.Should().BeEmpty();
    }

    internal static MetadataReference[] References => ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator).Where(path => !Path.GetFileName(path).StartsWith("DRN.", StringComparison.Ordinal))
        .Append(typeof(SensitiveAttribute).Assembly.Location)
        .Append(typeof(NSubstitute.SubstituteExtensions).Assembly.Location)
        .Append(typeof(Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder).Assembly.Location)
        .Append(typeof(Microsoft.EntityFrameworkCore.DbContext).Assembly.Location)
        .Append(typeof(Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute).Assembly.Location)
        .Append(typeof(Microsoft.Extensions.Logging.ILogger).Assembly.Location)
        .Distinct(StringComparer.Ordinal).Select(path => MetadataReference.CreateFromFile(path)).ToArray();

    internal static CSharpCompilation Compilation(string source, string path = "Models.cs") => CSharpCompilation.Create(
        "DiagnosticModelConsumer", [CSharpSyntaxTree.ParseText(source, path: path)], References,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    internal static async Task<ImmutableArray<Diagnostic>> Analyze(CSharpCompilation compilation, params AdditionalText[] files)
    {
        compilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        return await compilation.WithAnalyzers([new DiagnosticClassificationAnalyzer()], new AnalyzerOptions([.. files]))
            .GetAnalyzerDiagnosticsAsync();
    }

    [Fact]
    public async Task All_Models_Should_Be_Checked_Without_Logging_And_IgnoreLog_Should_Not_Suppress()
    {
        var diagnostics = await Analyze(Compilation("""
            using DRN.Framework.SharedKernel.Attributes;
            [IgnoreLog] public class Model
            {
                [IgnoreLog] public string Password { get; set; } = "";
                public string Keyboard { get; set; } = "";
                public object CancellationToken { get; set; } = new();
                public string Description { get; set; } = "";
                public static string AccessToken { get; set; } = "";
                public string PasswordField = "";
                private string Secret { get; set; } = "";
                public string PasswordWriteOnly { set { } }
                public string this[int index] => "";
            }
            """));
        diagnostics.Should().HaveCount(3).And.OnlyContain(item => item.Id == "DRN0015" && item.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task Derived_Classifications_And_Explicit_False_Should_Pass()
    {
        var diagnostics = await Analyze(Compilation("""
            using DRN.Framework.SharedKernel.Attributes;
            public class CompanyAttribute : SensitiveAttribute;
            public class RestrictedAttribute : CompanyAttribute;
            public class ContactAttribute : PersonalAttribute;
            public class Model
            {
                [Restricted] public string Password { get; set; } = "";
                [Contact] public string Email { get; set; } = "";
                [Sensitive(false)] public string Keyboard { get; set; } = "";
                [IgnoreLog, Sensitive] public string AccessToken => throw new System.Exception();
            }
            [Company] public class ClassifiedModel { public string Secret { get; set; } = ""; }
            """));
        diagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Models.cs", 4)]
    [InlineData("Models.g.cs", 0)]
    public async Task Inheritance_Generics_And_Records_Should_Report_Only_In_User_Code(string path, int expectedCount)
    {
        var diagnostics = await Analyze(Compilation("""
            using DRN.Framework.SharedKernel.Attributes;
            public class Base
            {
                [Sensitive] public virtual string Password { get; set; } = "";
                [Sensitive(false)] public virtual string Keyboard { get; set; } = "";
            }
            public class Child : Base
            {
                [Sensitive(false)] public override string Password { get; set; } = "";
                public override string Keyboard { get; set; } = "";
            }
            public record Contact(string Email);
            public class Box<T> { public T AccessToken { get; set; } = default!; }
            public interface IInput { string Password { get; } }
            """, path));
        diagnostics.Select(item => item.Id).Should().Equal(Enumerable.Repeat("DRN0015", expectedCount));
    }

    [Fact]
    public async Task Metadata_Derived_Classifications_Should_Be_Accepted()
    {
        var producer = Compilation("""
            using DRN.Framework.SharedKernel.Attributes;
            public class CompanyAttribute : SensitiveAttribute;
            public class ContactAttribute : PersonalAttribute;
            """).WithAssemblyName("Company.Classification");
        using var stream = new MemoryStream();
        producer.Emit(stream).Success.Should().BeTrue();
        var consumer = Compilation("""
            public class Model
            {
                [Company] public string Password { get; set; } = "";
                [Contact] public string Email { get; set; } = "";
            }
            """).AddReferences(MetadataReference.CreateFromImage(stream.ToArray()));
        (await Analyze(consumer)).Should().BeEmpty();
    }

    [Fact]
    public async Task Unrelated_Microsoft_Classification_Should_Not_Be_Assumed_NonNone()
    {
        var diagnostics = await Analyze(Compilation("""
            using Microsoft.Extensions.Compliance.Classification;
            public class CustomAttribute() : DataClassificationAttribute(DataClassification.None);
            public class Model { [Custom] public string Password { get; set; } = ""; }
            """));
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRN0018");
    }

    [Fact]
    public async Task False_On_A_Type_Should_Be_Rejected()
    {
        var diagnostics = await Analyze(Compilation("""
            using DRN.Framework.SharedKernel.Attributes;
            [Sensitive(false)] public class Model { public string Description { get; set; } = ""; }
            """));
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRN0016");
    }

    [Fact]
    public async Task Typed_Prefixes_Should_Not_Produce_Path_Errors_While_Anonymous_Paths_Remain_Checked()
    {
        var diagnostics = await Analyze(Compilation("""
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog { void AddProperties<T>(string prefix, T value) where T : class; }
            }
            public class Leaf { public string Ret { get; set; } = ""; }
            public class Model { public Leaf Secret { get; set; } = new(); }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log, Leaf value)
                {
                    log.AddProperties("ClientError", value);
                    log.AddProperties("Sec", value);
                    log.AddProperties("Pass", value);
                    log.AddProperties("se", new { Cret = "probe" });
                }
            }
            """));
        diagnostics.Count(item => item.Id == "DRN0017").Should().Be(1);
        diagnostics.Count(item => item.Id == "DRN0015").Should().Be(1);
    }

    [Fact]
    public async Task Capture_Analysis_Should_Distinguish_Typed_Models_Untyped_Values_And_Reason_Overloads()
    {
        var diagnostics = await Analyze(Compilation("""
            using DRN.Framework.SharedKernel.Attributes;
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog
                {
                    void Add(string key, object value);
                    void Add(string key, object value, string exemptionReason);
                    void AddProperties<T>(string prefix, T value) where T : class;
                }
            }
            public class Model
            {
                [Sensitive(false)] public int StatusCode => 503;
                [Sensitive] public string Password => throw new System.Exception();
                public Detail Details => new();
            }
            public class Detail { public int Count => 3; }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log, Model value)
                {
                    log.Add("SecretReport", value);
                    log.AddProperties("SecretReport", value);
                    log.Add("StatusCode", 503, exemptionReason: "HTTP status.");
                    log.Add("SecretReport", new { Count = 3 }, exemptionReason: "Inspect report root.");
                    log.Add("StatusCode", 503);
                    log.Add("SecretReport", new { Count = 3 });
                    log.Add("SecretReport", new System.Collections.Generic.Dictionary<string, object>());
                }
            }
            """));
        diagnostics.Should().HaveCount(3).And.OnlyContain(item => item.Id == "DRN0017");
    }

    [Fact]
    public async Task Date_Only_Time_Only_And_Uri_Should_Be_Analyzed_As_Scalar_Entries()
    {
        var diagnostics = await Analyze(Compilation("""
            namespace DRN.Framework.Utils.Logging
            {
                public interface IScopedLog
                {
                    void Add(string key, object value);
                    void Add(string key, object value, string exemptionReason);
                }
            }
            public class Caller
            {
                public void Run(DRN.Framework.Utils.Logging.IScopedLog log)
                {
                    log.Add("SecretDate", new System.DateOnly(2026, 1, 1));
                    log.Add("SecretTime", new System.TimeOnly(12, 30));
                    log.Add("SecretTarget", new System.Uri("https://example.test/"));
                    log.Add("SecretDate", new System.DateOnly(2026, 1, 1), "Reviewed date.");
                    log.Add("SecretTime", new System.TimeOnly(12, 30), "Reviewed time.");
                    log.Add("SecretTarget", new System.Uri("https://example.test/"), "Reviewed target.");
                }
            }
            """));
        diagnostics.Should().HaveCount(3).And.OnlyContain(item => item.Id == "DRN0017");
    }

    [Fact]
    public async Task Invalid_Derived_False_Constructor_Should_Be_Rejected()
    {
        var diagnostics = await Analyze(Compilation("""
            using DRN.Framework.SharedKernel.Attributes;
            public class InvalidAttribute : SensitiveAttribute
            {
                public InvalidAttribute() : base(false) { }
            }
            """));
        diagnostics.Should().Contain(item => item.Id == "DRN0016");
    }
}
