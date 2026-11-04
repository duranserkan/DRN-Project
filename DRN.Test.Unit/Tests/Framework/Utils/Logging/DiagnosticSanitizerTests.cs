using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.Utils;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Logging.Diagnostics;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static DRN.Test.Unit.Tests.Framework.Utils.Logging.DiagnosticTestSupport;

namespace DRN.Test.Unit.Tests.Framework.Utils.Logging;

public class DiagnosticSanitizerTests
{
    private static readonly string[] CapturedPropertyNames = ["Description", "Password"];
    private static readonly string[] ScopeWorkLimitWarnings = ["ScopeWorkLimit"];
    private static readonly string[] SanitizationFailedWarnings = ["SanitizationFailed"];

    private static DiagnosticSanitizer Policy(DiagnosticLogOptions? options = null) => new(options ?? new DiagnosticLogOptions());

    [Fact]
    public void Keys_Should_Not_Consume_Or_Depend_On_Input_Budget()
    {
        var options = new DiagnosticLogOptions { MaxFieldBytes = 1024, MaxInputBytes = 1024, MaxProcessingMilliseconds = 100 };
        var policy = Policy(options);
        var key = new string('x', 256);
        for (var index = 0; index < 5; index++)
            policy.SanitizeKey(key).Should().Be(key);
        var text = new string('v', 1024);
        policy.SanitizeText(text).Should().Be(text);
        policy.SanitizeText("overflow").Should().Be(DiagnosticSanitizer.Limited);
        policy.SanitizeKey(key).Should().Be(key);

        // Include the prefix and separator in the independent field-path limit.
        var captureKey = key[..(options.MaxKeyBytes - "Data.".Length)];
        var captured = JsonSerializer.SerializeToElement(Policy(options).SanitizeValue("Data",
            new Dictionary<string, object> { [captureKey] = text }));
        captured.GetProperty(captureKey).GetString().Should().Be(text);

        var oversizedPath = JsonSerializer.SerializeToElement(Policy(options).SanitizeValue("Data",
            new Dictionary<string, object> { [key] = text }));
        oversizedPath.GetProperty(key).GetString().Should().Be(DiagnosticSanitizer.Limited);
    }

    [Fact]
    public void Invalid_Keys_Should_Keep_Generated_Fallbacks()
    {
        var policy = Policy(new DiagnosticLogOptions { MaxKeyBytes = 32, MaxProcessingMilliseconds = 100 });
        foreach (var key in new[] { new string('x', 33), new string('é', 17), "bad\nname", "_diagnostic_reserved" })
        {
            policy.SanitizeKey(key).Should().StartWith("_diagnostic_field_");
            var captured = JsonSerializer.SerializeToElement(policy.SanitizeValue("Data",
                new Dictionary<string, object> { [key] = 1 }));
            captured.EnumerateObject().Single().Name.Should().StartWith("_diagnostic_field_");
        }
    }

    [Fact]
    [SuppressMessage("DRN.Classification", "DRN0017", Justification = "Unclassified Password capture verifies free text is preserved without runtime name matching.")]
    public void Free_Text_Should_Preserve_Content_Across_Capture_Paths()
    {
        string[] cases =
        [
            "token=opaque-probe",
            "password=opaque-probe",
            "Cookie: first=opaque-probe; second=other-probe",
            "Bearer opaque-probe",
            "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.signature",
            "user@example.com:opaque-probe",
            "-----BEGIN PRIVATE KEY-----\nopaque-probe\n-----END PRIVATE KEY-----",
            "Failure at https://example.test/token/opaque-probe"
        ];
        foreach (var text in cases)
        {
            var policy = Policy();
            policy.SanitizeText(text).Should().BeSameAs(text);
            policy.SanitizeValue("Message", text).Should().Be(text);
            policy.SanitizeBody(text, "text/plain").Should().Be(text);
            policy.SanitizeValue("Password", text).Should().Be(text);
        }
    }

    [Fact]
    public void Runtime_Field_And_Url_Names_Should_Not_Be_Classified()
    {
        var policy = Policy(new DiagnosticLogOptions
        {
            SensitiveFields = ["opaque", "---"],
            SensitivePathSegments = ["handoff", "---"]
        });
        policy.IsSensitive("Handoff.Count").Should().BeFalse();
        policy.IsSensitive("Root.O-P_A.QuE.Value").Should().BeFalse();
        policy.SanitizeUrl("/opaque/public-value").Should().Be("/opaque/public-value");
        policy.SanitizeUrl("/v2-H-A_N.Doff-flow/opaque-probe")
            .Should().Be("/v2-H-A_N.Doff-flow/opaque-probe");
        policy.SanitizeBody("code=opaque-probe&count=2", "application/x-www-form-urlencoded")
            .Should().Be("code=opaque-probe&count=2");
    }

    [Fact]
    public void Form_Bodies_Should_Ignore_Configured_Source_Paths()
    {
        var options = new DiagnosticLogOptions { SensitiveFieldPaths = ["Checkout.Value"] };
        const string form = "%56alue=private&Count=2&Password=secret";

        foreach (var path in new[] { "/Checkout", "/Other", "" })
        {
            Policy(options).SanitizeBody(form, "application/x-www-form-urlencoded", path)
                .Should().Be("Value=private&Count=2&Password=secret");
        }

        Policy(options).SanitizeUrl("/Checkout?Value=public&Password=secret")
            .Should().Be("/Checkout?Value=public&Password=secret");
    }

    [Fact]
    public void Long_Url_Segments_Should_Be_Preserved_Without_Name_Matching()
    {
        var segment = new string('x', 600) + "resetpassword";
        Policy().SanitizeUrl("/" + segment + "/opaque-probe")
            .Should().Be("/" + segment + "/opaque-probe");
    }

    [Fact]
    public void Runtime_Name_Queries_Should_Not_Classify_Any_Name()
    {
        var sanitizer = Policy();
        foreach (var name in new[] { "Phone.Password", "Contact.Phone", "Headers.Authorization", new string('x', 513) })
            sanitizer.IsSensitive(name).Should().BeFalse();
    }

    [Fact]
    public void Runtime_Configured_Rules_Should_Not_Match_Normalized_Substrings()
    {
        var policy = Policy(new DiagnosticLogOptions
        {
            SensitiveFields = ["opaque", "---"],
            SensitiveFieldPaths = ["Checkout.Value"],
            SensitivePathSegments = ["handoff"]
        });
        policy.SanitizeValue("CustomOpaqueSuffix", "probe").Should().Be("probe");
        policy.SanitizeValue("Root.Checkout.ValueSuffix", "probe").Should().Be("probe");
        policy.SanitizeValue("Result.Count", 2).Should().Be(2);
        policy.SanitizeUrl("/v2-handoff-flow/probe?attempt=2")
            .Should().Be("/v2-handoff-flow/probe?attempt=2");
    }

    [Fact]
    public void Compliance_Classifications_Should_Use_The_Diagnostic_Redactor()
    {
        new SensitiveAttribute().Classification.Should().Be(DataClassifications.Sensitive);
        new PersonalAttribute().Classification.Should().Be(DataClassifications.Personal);

        using var services = new ServiceCollection().AddDrnUtils().BuildServiceProvider();
        var provider = services.GetRequiredService<IRedactorProvider>();
        provider.GetRedactor(DataClassifications.Sensitive).Redact("secret-probe").Should().Be(DiagnosticSanitizer.Redacted);
        provider.GetRedactor(DataClassifications.Personal).Redact("personal-probe").Should().Be(DiagnosticSanitizer.Redacted);
        provider.GetRedactor(DataClassifications.Set).Redact("combined-probe").Should().Be(DiagnosticSanitizer.Redacted);
    }

    [Fact]
    public void Compliance_Attributes_Should_Redact_Captured_Values_Without_Recapturing_Getters()
    {
        var input = new ComplianceData();
        var sanitized = (IReadOnlyDictionary<string, object>)Policy().SanitizeValue("Direct", input)!;
        input.GetterReads.Should().Be(0);
        using var settings = SettingsProvider.Development();
        var log = new ScopedLog(settings, Policy());
        log.Add("Nested", input);
        input.GetterReads.Should().Be(3);
        log.AddProperties("Flat", input);
        input.GetterReads.Should().Be(6);
        var snapshot = log.GetLogs();
        var nested = (IReadOnlyDictionary<string, object>)snapshot["Nested"];
        foreach (var name in new[] { "Contact", "Custom", "Typed" })
        {
            sanitized[name].Should().Be(DiagnosticSanitizer.Redacted);
            nested[name].Should().Be(DiagnosticSanitizer.Redacted);
            snapshot[$"Flat.{name}"].Should().Be(DiagnosticSanitizer.Redacted);
        }
        nested["Ignored"].Should().Be(ScopedLogConventions.IgnoredLogValue);
        snapshot["Flat.Ignored"].Should().Be(ScopedLogConventions.IgnoredLogValue);
        nested["Outcome"].Should().Be("Processed");
        snapshot["Flat.Outcome"].Should().Be("Processed");
        nested["Hint"].Should().Be("Retry available");
        snapshot["Flat.Hint"].Should().Be("Retry available");
        var raw = log.GetLogs(redact: false);
        var rawNested = (IReadOnlyDictionary<string, object>)raw["Nested"];
        rawNested["Contact"].Should().Be("contact-probe");
        rawNested["Custom"].Should().Be("custom-probe");
        raw["Flat.Contact"].Should().Be("contact-probe");
        raw["Flat.Custom"].Should().Be("custom-probe");
        rawNested["Ignored"].Should().Be(ScopedLogConventions.IgnoredLogValue);
        raw["Flat.Ignored"].Should().Be(ScopedLogConventions.IgnoredLogValue);
        log.ToString().Should().NotContain("contact-probe").And.NotContain("custom-probe");
        input.GetterReads.Should().Be(6);
    }

    private sealed class ConfidentialAttribute() : DataClassificationAttribute(new DataClassification("Test", "Confidential"));
    private sealed class UnclassifiedAttribute() : DataClassificationAttribute(DataClassification.None);

    [Personal]
    private sealed class PersonalRecord { }

    private sealed class ComplianceData
    {
        public int GetterReads { get; private set; }
        [Personal] public string Contact { get { GetterReads++; return "contact-probe"; } }
        [Confidential] public string Custom { get { GetterReads++; return "custom-probe"; } }
        public PersonalRecord Typed { get { GetterReads++; return new PersonalRecord(); } }
        [IgnoreLog, Personal] public string Ignored { get { GetterReads++; return "ignored-probe"; } }
        [SuppressMessage("Performance", "CA1822", Justification = "Instance property exercises reflection capture of an unclassified value.")]
        public string Outcome => "Processed";
        [SuppressMessage("Performance", "CA1822", Justification = "Instance property exercises capture with an empty classification.")]
        [Unclassified] public string Hint => "Retry available";
    }

    [Fact]
    public void Runtime_Cases_Should_Preserve_Unclassified_Values_And_Explicit_Url_Processing()
    {
        using var stream = typeof(DiagnosticSanitizerTests).Assembly.GetManifestResourceStream("DiagnosticRuntimeCases.json")!;
        using var cases = JsonDocument.Parse(stream);
        foreach (var item in cases.RootElement.EnumerateArray())
        {
            var sanitizer = Policy();
            var value = item.GetProperty("value");
            object? actual = item.GetProperty("kind").GetString() switch
            {
                "field" => sanitizer.SanitizeValue(item.GetProperty("field").GetString()!, value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString(),
                    JsonValueKind.Number => (object)value.GetDecimal(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    JsonValueKind.Array => value.EnumerateArray().Select(element => element.GetString()).ToArray(),
                    _ => throw new InvalidOperationException("Golden field cases must supply scalars or string arrays.")
                }),
                "url" => sanitizer.SanitizeUrl(value.GetString()),
                _ => sanitizer.SanitizeText(value.GetString())
            };
            JsonElement.DeepEquals(JsonSerializer.SerializeToElement(actual), item.GetProperty("expected"))
                .Should().BeTrue("golden case {0} should match", item.ToString());
            if (item.TryGetProperty("limited", out var limited))
                (sanitizer.GetStatus().Count > 0).Should().Be(limited.GetBoolean());
            if (item.GetProperty("kind").GetString() == "text")
                Policy().SanitizeText((string)actual!).Should().Be((string)actual!);
        }
    }

    [Fact]
    public void Classified_And_Ignored_Getters_Should_Not_Execute()
    {
        var sanitizer = Policy();
        var data = (IReadOnlyDictionary<string, object?>)sanitizer.SanitizeValue("Input", new Classified())!;
        data["Code"].Should().Be(DiagnosticSanitizer.Redacted);
        data["Ignored"].Should().Be(ScopedLogConventions.IgnoredLogValue);
        data["HasRecoveryCodes"].Should().Be(DiagnosticSanitizer.Redacted);
        data["Failure"].Should().Be("[diagnostic field unavailable]");
        var log = new ScopedLog(SettingsProvider.Development(), Policy());
        log.AddProperties("Input", new Classified());
        log.Add("Nested", new Classified());
        log.GetLogs()["Input.Code"].Should().Be(DiagnosticSanitizer.Redacted);
        log.GetLogs()["Input.Ignored"].Should().Be(ScopedLogConventions.IgnoredLogValue);
        var nested = (IReadOnlyDictionary<string, object?>)log.GetLogs()["Nested"];
        nested["Code"].Should().Be(DiagnosticSanitizer.Redacted);
        nested["HasRecoveryCodes"].Should().Be(DiagnosticSanitizer.Redacted);
        nested["Ignored"].Should().Be(ScopedLogConventions.IgnoredLogValue);
        nested["Failure"].Should().Be(DiagnosticSanitizer.Invalid);
        var raw = (IReadOnlyDictionary<string, object?>)log.GetLogs(false)["Nested"];
        raw["Code"].Should().Be(DiagnosticSanitizer.Invalid);
        raw["HasRecoveryCodes"].Should().Be(DiagnosticSanitizer.Invalid);
    }

    [Fact]
    public void SensitiveData_Should_Support_Types_And_Properties_With_Ignore_Precedence()
    {
        var policy = Policy();
        policy.SanitizeValue("Custom", new SensitiveType()).Should().Be(DiagnosticSanitizer.Redacted);
        var properties = (IReadOnlyDictionary<string, object?>)policy.SanitizeValue("Custom", new SensitiveProperties())!;
        properties["Value"].Should().Be(DiagnosticSanitizer.Redacted);
        properties["Contact"].Should().Be(DiagnosticSanitizer.Redacted);
        properties["Ignored"].Should().Be(ScopedLogConventions.IgnoredLogValue);
        var log = new ScopedLog(SettingsProvider.Development(), Policy());
        log.AddProperties("Custom", new SensitiveType());
        log.GetLogs()["Custom"].Should().Be(DiagnosticSanitizer.Redacted);
    }

    [Sensitive]
    private sealed class SensitiveType
    {
        [SuppressMessage("Performance", "CA1822", Justification = "Instance getter verifies classified types are not traversed.")]
        public string Value => throw new InvalidOperationException("Classified type getter must not execute.");
    }

    private sealed class SensitiveProperties
    {
        [SuppressMessage("Performance", "CA1822", Justification = "Instance getter verifies sensitive classification prevents reads.")]
        [Sensitive] public string Value => throw new InvalidOperationException("Sensitive getter must not execute.");
        [SuppressMessage("Performance", "CA1822", Justification = "Instance getter verifies personal classification prevents reads.")]
        [Personal] public string Contact => throw new InvalidOperationException("Personal getter must not execute.");
        [SuppressMessage("Performance", "CA1822", Justification = "Instance getter verifies IgnoreLog takes precedence over classification.")]
        [IgnoreLog, Sensitive] public string Ignored => throw new InvalidOperationException("Ignored getter must not execute.");
    }

    [Fact]
    [SuppressMessage("DRN.Classification", "DRN0017", Justification = "Unclassified Password entry is the control for explicitly classified object properties.")]
    [SuppressMessage("DRN.Classification", "DRN0019", Justification = "Anonymous Password fixture verifies JsonElement projection independently of classified model capture.")]
    [SuppressMessage("DRN.Classification", "DRN0020", Justification = "Unclassified Password dictionary entry is the control for classified nested properties.")]
    public void Object_Logging_Should_Omit_Fields_And_Preserve_Explicit_Classification()
    {
        var input = new PropertyCapture();
        var policy = Policy();
        var projected = (IReadOnlyDictionary<string, object?>)policy.SanitizeValue("Input", input)!;
        projected.Keys.Should().BeEquivalentTo(CapturedPropertyNames);
        projected["Description"].Should().Be("available");
        projected["Password"].Should().Be(DiagnosticSanitizer.Redacted);

        var log = new ScopedLog(SettingsProvider.Development(), Policy());
        log.Add("Nested", input);
        log.AddProperties("Flat", input);
        log.Add("Password", "named-probe");
        log.Add("Map", new Dictionary<string, object> { ["Password"] = "dictionary-probe", ["Item"] = input });
        log.Add("Json", JsonSerializer.SerializeToElement(new { Password = "json-probe", Description = "available" },
            FixtureJsonOptions));

        var snapshot = JsonSerializer.SerializeToElement(log.GetLogs(), ProjectionJsonOptions);
        snapshot.GetProperty("Nested").EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo(CapturedPropertyNames);
        snapshot.GetProperty("Flat.Description").GetString().Should().Be("available");
        snapshot.GetProperty("Flat.Password").GetString().Should().Be(DiagnosticSanitizer.Redacted);
        snapshot.GetProperty("Password").GetString().Should().Be("named-probe");
        snapshot.GetProperty("Map").GetProperty("Password").GetString().Should().Be("dictionary-probe");
        snapshot.GetProperty("Map").GetProperty("Item").EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo(CapturedPropertyNames);
        snapshot.GetProperty("Json").GetProperty("ValueKind").GetInt32().Should().Be((int)JsonValueKind.Object);
        snapshot.ToString().Should().NotContain("PublicValue").And.NotContain("ClassifiedValue")
            .And.NotContain("IgnoredValue");
    }

    private sealed class PropertyCapture
    {
        public readonly string PublicValue = "public-field-probe";
        [Sensitive] public readonly string ClassifiedValue = "classified-field-probe";
        [IgnoreLog] public readonly string IgnoredValue = "ignored-field-probe";
        [SuppressMessage("Performance", "CA1822", Justification = "Instance property verifies capture includes properties while omitting fields.")]
        public string Description => "available";
        [SuppressMessage("Performance", "CA1822", Justification = "Instance getter verifies classified properties are not read.")]
        [Sensitive] public string Password => throw new InvalidOperationException("Classified getter must not execute.");
    }

    private sealed class Classified
    {
        [SuppressMessage("Performance", "CA1822", Justification = "Instance getter verifies sensitive classification prevents reads.")]
        [Sensitive] public string Code => throw new InvalidOperationException("secret-getter");
        [SuppressMessage("Performance", "CA1822", Justification = "Instance getter verifies IgnoreLog prevents reads.")]
        [IgnoreLog] public string Ignored => throw new InvalidOperationException("ignored-getter");
        [SuppressMessage("Performance", "CA1822", Justification = "Instance getter verifies classified Boolean properties are protected.")]
        [Sensitive] public bool HasRecoveryCodes => throw new InvalidOperationException("Classified getter must not execute.");
        [SuppressMessage("Performance", "CA1822", Justification = "Instance getter exercises capture failure handling.")]
        public string Failure => throw new InvalidOperationException("getter-message-probe");
    }

    [Fact]
    public void Overridden_Classified_Getters_Should_Retain_Classification_After_Capture()
    {
        var input = new ClassifiedOverride();
        var sanitized = (IReadOnlyDictionary<string, object>)Policy().SanitizeValue("Direct", input)!;
        sanitized["Code"].Should().Be(DiagnosticSanitizer.Redacted);
        sanitized["Contact"].Should().Be(DiagnosticSanitizer.Redacted);
        sanitized["Ignored"].Should().Be(ScopedLogConventions.IgnoredLogValue);
        input.GetterReads.Should().Be(0);
        using var settings = SettingsProvider.Development();
        var log = new ScopedLog(settings, Policy());
        log.Add("Nested", input);
        input.GetterReads.Should().Be(2);
        log.AddProperties("Flat", input);
        input.GetterReads.Should().Be(4);

        var snapshot = log.GetLogs();
        var nested = (IReadOnlyDictionary<string, object>)snapshot["Nested"];
        nested["Code"].Should().Be(DiagnosticSanitizer.Redacted);
        nested["Contact"].Should().Be(DiagnosticSanitizer.Redacted);
        nested["Ignored"].Should().Be(ScopedLogConventions.IgnoredLogValue);
        snapshot["Flat.Code"].Should().Be(DiagnosticSanitizer.Redacted);
        snapshot["Flat.Contact"].Should().Be(DiagnosticSanitizer.Redacted);
        snapshot["Flat.Ignored"].Should().Be(ScopedLogConventions.IgnoredLogValue);
        var raw = log.GetLogs(redact: false);
        var rawNested = (IReadOnlyDictionary<string, object>)raw["Nested"];
        rawNested["Code"].Should().Be("override-probe");
        rawNested["Contact"].Should().Be("override-probe");
        raw["Flat.Code"].Should().Be("override-probe");
        raw["Flat.Contact"].Should().Be("override-probe");
        rawNested["Ignored"].Should().Be(ScopedLogConventions.IgnoredLogValue);
        raw["Flat.Ignored"].Should().Be(ScopedLogConventions.IgnoredLogValue);
        log.ToString().Should().NotContain("override-probe");
        input.GetterReads.Should().Be(4);
    }

    private class ClassifiedBase
    {
        [Sensitive] public virtual string Code => "base-probe";
        [Personal] public virtual string Contact => "base-probe";
        [IgnoreLog, Sensitive] public virtual string Ignored => "base-probe";
    }

    private sealed class ClassifiedOverride : ClassifiedBase
    {
        public int GetterReads { get; private set; }
        public override string Code { get { GetterReads++; return "override-probe"; } }
        public override string Contact { get { GetterReads++; return "override-probe"; } }
        public override string Ignored { get { GetterReads++; return "override-probe"; } }
    }

    [Fact]
    public void Url_Strings_Should_Remain_Text_In_Dtos_And_Dictionaries()
    {
        var dto = new { Url = "/User/ConfirmEmail?code=url-probe&id=42", Count = 2 };
        var dictionary = new Dictionary<string, object> { ["Url"] = dto.Url, ["Count"] = dto.Count };
        object[] inputs = [dto, dictionary];
        foreach (var input in inputs)
        {
            var safe = JsonSerializer.SerializeToElement(Policy().SanitizeValue("Report", input));
            safe.GetProperty("Url").GetString().Should().Be(dto.Url);
            safe.GetProperty("Count").GetInt32().Should().Be(2);
        }
    }

    [Fact]
    public void Forms_And_Urls_Should_Validate_Encoding_Without_Name_Matching()
    {
        var policy = Policy();
        policy.SanitizeUrl("https://example.test/path?value=opaque%2Fprobe")
            .Should().Be("https://example.test/path?value=opaque%2Fprobe");
        policy.SanitizeUrl("/token/opaque-probe?count=2").Should().Be("/token/opaque-probe?count=2");
        policy.SanitizeUrl("/token%3Dopaque-probe").Should().Be("/token%3Dopaque-probe");
        policy.SanitizeBody("message=password%3Dprobe&password=secret", "application/x-www-form-urlencoded")
            .Should().Be("message=password%3Dprobe&password=secret");
        policy.SanitizeBody("QrCodeVerify.Code=123456&count=2", "application/x-www-form-urlencoded")
            .Should().Be("QrCodeVerify.Code=123456&count=2");
        policy.SanitizeUrl("https://example.test/?value=%GGsecret-probe").Should().NotContain("secret-probe");
        Policy().SanitizeText("opaque/probe").Should().Be("opaque/probe");
    }

    [Theory]
    [DataInlineUnit("application/json")]
    [DataInlineUnit("application/problem+json")]
    [DataInlineUnit("application/octet-stream")]
    public void Unsupported_Bodies_Should_Not_Be_Parsed_Or_Exposed(string contentType)
    {
        Policy().SanitizeBody("{\"Password\":\"private-probe\"}", contentType)
            .Should().Be("[unsupported diagnostic body]");
        Policy().SanitizeBody("{malformed private-probe", contentType)
            .Should().Be("[unsupported diagnostic body]");
        Policy().SanitizeBody("", contentType).Should().BeEmpty();
    }

    [Fact]
    public void Capture_Should_Preserve_Scalars_And_Project_Ordinary_Public_Properties()
    {
        var policy = Policy(new DiagnosticLogOptions { MaxProcessingMilliseconds = 100 });
        policy.SanitizeValue("Value", null).Should().BeNull();
        policy.SanitizeValue("Count", 42).Should().Be(42);
        policy.SanitizeValue("Value", "visible").Should().Be("visible");
        using var document = JsonDocument.Parse("""{"Count":2,"Password":"private"}""");
        var result = JsonSerializer.SerializeToElement(policy.SanitizeValue("Report", document.RootElement), ProjectionJsonOptions);
        result.GetProperty("ValueKind").GetInt32().Should().Be((int)JsonValueKind.Object);
        result.TryGetProperty("Count", out _).Should().BeFalse();
        result.TryGetProperty("Password", out _).Should().BeFalse();
        var container = JsonSerializer.SerializeToElement(policy.SanitizeValue("Document", document), ProjectionJsonOptions);
        container.GetProperty("RootElement").GetProperty("ValueKind").GetInt32().Should().Be((int)JsonValueKind.Object);
        policy.SanitizeValue("Items", new System.Text.Json.Nodes.JsonArray(1, 2))
            .Should().Be("[unsupported lazy diagnostic collection]");
    }

    [Fact]
    public void Capture_Should_Distinguish_Shared_Children_From_Ancestor_Cycles()
    {
        var policy = Policy(new DiagnosticLogOptions { MaxProcessingMilliseconds = 100 });
        var child = new List<object> { 42 };
        var root = new List<object> { child, child };
        root.Add(root);

        var result = JsonSerializer.SerializeToElement(policy.SanitizeValue("Items", root));
        result.GetArrayLength().Should().Be(3);
        result[0][0].GetInt32().Should().Be(42);
        result[1][0].GetInt32().Should().Be(42);
        result[2].GetString().Should().Be("[diagnostic cycle]");
    }

    [Fact]
    public void Reentrant_Capture_Should_Use_Independent_Ancestor_Tracking()
    {
        var policy = Policy(new DiagnosticLogOptions { MaxProcessingMilliseconds = 100 });
        var root = new List<object>();
        var reading = false;
        object? reentrantResult = null;
        root.Add(new ReentrantCapture(() =>
        {
            if (reading) return 42;
            reading = true;
            try
            {
                reentrantResult = policy.SanitizeValue("Items", root);
                return 42;
            }
            finally { reading = false; }
        }));

        var result = JsonSerializer.SerializeToElement(policy.SanitizeValue("Items", root));
        result[0].GetProperty("Value").GetInt32().Should().Be(42);
        reentrantResult.Should().NotBeNull();
        var nested = JsonSerializer.SerializeToElement(reentrantResult);
        nested[0].GetProperty("Value").GetInt32().Should().Be(42);
    }

    // Object-typed properties are intentionally ignored by diagnostic capture.
    private sealed class ReentrantCapture(Func<int> read)
    {
        public int Value => read();
    }

    [Fact]
    public void Capture_Should_Bound_Fields_Cycles_Collections_And_Repeated_Work()
    {
        var policy = Policy(new DiagnosticLogOptions { MaxFieldBytes = 1024, MaxInputBytes = 2048, MaxCollectionEntries = 4 });
        policy.SanitizeText(new string('x', 1025)).Should().Be(DiagnosticSanitizer.Limited);
        var cyclic = new List<object>();
        cyclic.Add(cyclic);
        JsonSerializer.Serialize(policy.SanitizeValue("Items", cyclic)).Should().Contain("diagnostic cycle");
        var values = Enumerable.Range(0, 20).ToArray();
        ((IList<object?>)policy.SanitizeValue("Items", values)!).Should().HaveCount(5);
        Parallel.For(0, 12, _ => policy.SanitizeText(new string('a', 1000)));
        policy.SanitizeText("last-probe").Should().Be(DiagnosticSanitizer.Limited);
        policy.GetStatus().Should().ContainKey("DiagnosticCaptureWarnings");
    }

    [Fact]
    public void Url_Pairs_Should_Share_The_Model_Capture_Node_Budget()
    {
        var policy = Policy(new DiagnosticLogOptions { MaxNodes = 16, MaxProcessingMilliseconds = 100 });
        for (var index = 0; index < 8; index++)
            policy.SanitizeValue("TargetUrl", new Uri("https://example.test/?value=probe")).Should().Be("https://example.test/?value=probe");

        policy.SanitizeValue("Count", 42).Should().Be(DiagnosticSanitizer.Limited);
        policy.GetStatus()["DiagnosticCaptureWarnings"].Should().BeEquivalentTo(ScopeWorkLimitWarnings);
    }

    [Theory]
    [DataInlineUnit("/%25GG")]
    [DataInlineUnit("/?value=%25GG")]
    [DataInlineUnit("/#value=%25GG")]
    public void Malformed_Encoded_Urls_Should_Fail_Closed_Without_Disabling_Later_Captures(string url)
    {
        var policy = Policy(new DiagnosticLogOptions { MaxProcessingMilliseconds = 100 });

        policy.SanitizeUrl(url).Should().Be(DiagnosticSanitizer.Invalid);
        policy.GetStatus()["DiagnosticCaptureWarnings"].Should().BeEquivalentTo(SanitizationFailedWarnings);
        policy.SanitizeUrl("/public?value=visible").Should().Be("/public?value=visible");
    }

    [Theory]
    [DataInlineUnit("/#token-probe", "/#token-probe")]
    [DataInlineUnit("/#token=probe&section=public", "/#token=probe&section=public")]
    public void Url_Fragments_Should_Preserve_Named_Values_And_Bare_Text(string url, string expected)
    {
        Policy(new DiagnosticLogOptions { MaxProcessingMilliseconds = 100 }).SanitizeUrl(url).Should().Be(expected);
    }

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public void Query_Node_Exhaustion_Should_Bound_Partial_And_Deferred_Captures(bool deferred)
    {
        var policy = Policy(new DiagnosticLogOptions { MaxNodes = 16, MaxProcessingMilliseconds = 100 });
        for (var index = 0; index < 14; index++) policy.SanitizeValue("Count", index).Should().Be(index);

        var capture = policy.Capture("TargetUrl", new Uri("https://example.test/?first=one&second=two"), deferRedaction: deferred);

        capture.Value.Should().Be(deferred ? DiagnosticSanitizer.Limited
            : "https://example.test/?first=one&_diagnostic=" + DiagnosticSanitizer.Limited);
        policy.GetStatus()["DiagnosticCaptureWarnings"].Should().BeEquivalentTo(ScopeWorkLimitWarnings);
    }

    [Fact]
    [SuppressMessage("DRN.Classification", "DRN0020", Justification = "Unclassified Password dictionary entry verifies detached copies preserve captured values without name matching.")]
    public void Scoped_Entries_Should_Be_Detached_And_Protect_All_Capture_Paths()
    {
        var policy = Policy();
        var log = new ScopedLog(SettingsProvider.Development(), policy);
        var input = new Dictionary<string, object> { ["Password"] = "dictionary-probe", ["Count"] = 2 };
        log.Add("Input", input);
        input["Count"] = "mutation-probe";
        log.AddToList("Actions", "token=list-probe");
        log.WithEvent(new ScopeEvent(new EventId(42, "Save"), Reason: "password=event-probe"));
        log.AddException(new InvalidOperationException("password=exception-probe"), "token=context-probe");
        log.Add("Stack", new string('s', 2000));
        var copy = new ScopedLog(SettingsProvider.Development(), Policy());
        copy.CopyFrom(log);
        var serialized = copy.ToString();
        serialized.Should().Contain("dictionary-probe").And.NotContain("mutation-probe");
        serialized.Should().Contain("list-probe").And.Contain("event-probe").And.Contain("exception-probe");
        copy.GetLogs()["Stack"].Should().Be(new string('s', 2000));
        copy.CorrelationId.Should().NotBe(log.CorrelationId);
        copy.EventId.Should().Be(42);
        var snapshot = (IDictionary<string, object>)log.GetLogs()["Input"];
        snapshot["Count"] = "snapshot-probe";
        log.ToString().Should().NotContain("snapshot-probe");
    }

    [Fact]
    [SuppressMessage("DRN.Classification", "DRN0020", Justification = "Unclassified key-probe entries verify key length validation without runtime name matching.")]
    public void Unsafe_Keys_And_External_Snapshots_Should_Not_Bypass_Policy()
    {
        var policy = Policy();
        var map = new Dictionary<string, object> { ["key-probe-a"] = 1, ["key-probe-b"] = 2, [new string('k', 1000)] = 3 };
        var safe = (IReadOnlyDictionary<string, object?>)policy.SanitizeValue("", map)!;
        safe.Should().HaveCount(3);
        safe.Keys.Should().OnlyContain(key => key.Length < 256);
        safe["key-probe-a"].Should().Be(1);
        safe["key-probe-b"].Should().Be(2);

        var foreign = Substitute.For<IScopedLog>();
        foreign.GetLogs().Returns(new Dictionary<string, object> { ["Password"] = "foreign-probe" });
        var logger = Substitute.For<ILogger>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        logger.LogScoped(foreign);
        var state = logger.ReceivedCalls().Single(call => call.GetMethodInfo().Name == "Log").GetArguments()[2];
        state!.ToString().Should().Contain("foreign-probe");
    }

    [Fact]
    public void Sink_And_Foreign_Policy_Failures_Should_Not_Replace_Application_Failures()
    {
        var log = new ScopedLog(SettingsProvider.Development(), Policy());
        var original = new InvalidOperationException("password=original-probe");
        log.AddException(original);
        Action emit = () => new FailedSink().LogScoped(log);
        emit.Should().NotThrow();
        original.Message.Should().Contain("original-probe");

        var foreign = Substitute.For<IScopedLog>();
        foreign.GetLogs().Returns(new Dictionary<string, object> { ["Message"] = "foreign-probe" });
        var policy = Substitute.For<IDiagnosticSanitizer>();
        policy.SanitizeValue(Arg.Any<string>(), Arg.Any<object>()).Returns(_ => throw new InvalidOperationException("policy-probe"));
        var logger = Substitute.For<ILogger>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        Action failedPolicy = () => logger.LogScoped(foreign, LogLevel.Error, policy);
        failedPolicy.Should().NotThrow();
        logger.ReceivedCalls().Should().NotContain(call => call.GetMethodInfo().Name == "Log");
    }

    private sealed class FailedSink : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => throw new InvalidOperationException("sink-probe");
    }
}
