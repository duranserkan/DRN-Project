using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Logging.Diagnostics;
using Microsoft.Extensions.Logging;
using static DRN.Test.Unit.Tests.Framework.Utils.Logging.DiagnosticTestSupport;

namespace DRN.Test.Unit.Tests.Framework.Utils.Logging;

public sealed class DiagnosticClassificationTests
{
    [Fact]
    public void Array_Property_Captures_Should_Preserve_Public_Properties()
    {
        using var settings = SettingsProvider.Development();
        var log = new ScopedLog(settings, Policy());
        var items = new[] { 1, 2 };

        log.AddProperties("Items", items);
        log.AddProperties<Array>("Declared", items);

        var snapshot = log.GetLogs();
        snapshot["Items.Length"].Should().Be(2);
        snapshot["Items.Rank"].Should().Be(1);
        snapshot["Declared.Length"].Should().Be(2);
        snapshot["Declared.Rank"].Should().Be(1);
        snapshot.Should().NotContainKey("Items");
        snapshot.Should().NotContainKey("Declared");
    }

    [Fact]
    public void Interface_Annotations_Should_Protect_Concrete_Nested_And_Declared_Captures()
    {
        var model = new InterfaceDerivedModel();
        var direct = JsonSerializer.SerializeToElement(Policy().SanitizeValue("Model", model));
        direct.GetProperty("Value").GetString().Should().Be(DiagnosticSanitizer.Redacted);
        direct.GetProperty("Hidden").GetString().Should().Be(ScopedLogConventions.IgnoredLogValue);
        direct.GetProperty("Keyboard").GetString().Should().Be("layout");
        direct.GetProperty("Ordinary").GetString().Should().Be("visible");
        model.ReadCount.Should().Be(0);

        using var settings = SettingsProvider.Development();
        var log = new ScopedLog(settings, Policy());
        log.AddProperties<IClassifiedValue>("Contract", model);
        log.AddProperties("Concrete", model);
        log.Add("Nested", new InterfaceContainer { Item = model });
        log.AddToList("Items", model);
        log.Add("Generic", new InterfaceGenericModel<int>());
        log.AddProperties<IClassifiedValue>("Explicit", new ExplicitInterfaceModel());
        model.ReadCount.Should().Be(4);

        var safe = JsonSerializer.Serialize(log.GetLogs());
        safe.Should().NotContain("interface-private-probe").And.Contain("visible");
        log.GetLogs()["Contract.Value"].Should().Be(DiagnosticSanitizer.Redacted);
        log.GetLogs()["Concrete.Value"].Should().Be(DiagnosticSanitizer.Redacted);
        log.GetLogs()["Explicit.Value"].Should().Be(DiagnosticSanitizer.Redacted);
        JsonSerializer.Serialize(log.GetLogs(false)).Should().Contain("interface-private-probe");
        model.ReadCount.Should().Be(4);

        // An unrelated explicit implementation must not classify a same-named public member.
        var separate = JsonSerializer.SerializeToElement(Policy().SanitizeValue("Model", new ExplicitInterfaceModel()));
        separate.GetProperty("Value").GetString().Should().Be("visible");
    }

    private interface IClassifiedValue
    {
        [Personal] string Value { get; }
    }

    private interface IClassifiedProjection : IClassifiedValue
    {
        [IgnoreLog] string Hidden { get; }
        [Sensitive(false)] string Keyboard { get; }
    }

    private class InterfaceModel : IClassifiedProjection
    {
        [IgnoreLog] public int ReadCount { get; protected set; }
        public virtual string Value { get { ReadCount++; return "interface-private-probe"; } }
        public string Hidden => throw new InvalidOperationException("Interface-ignored getter must not execute.");
        [SuppressMessage("DRN.Classification", "DRN0015", Justification = "Keep the implementation unannotated to test the interface-only exemption.")]
        public string Keyboard => "layout";
        [SuppressMessage("Performance", "CA1822", Justification = "Instance property exercises reflection capture.")]
        public string Ordinary => "visible";
    }

    private sealed class InterfaceDerivedModel : InterfaceModel
    {
        [Sensitive(false)] public override string Value => base.Value;
    }

    private sealed class InterfaceContainer
    {
        public IClassifiedValue Item { get; init; } = null!;
    }

    private sealed class InterfaceGenericModel<T> : IClassifiedValue
    {
        public string Value => "interface-private-probe";
    }

    private sealed class ExplicitInterfaceModel : IClassifiedValue
    {
        string IClassifiedValue.Value => "interface-private-probe";
        [SuppressMessage("Performance", "CA1822", Justification = "Public instance property must remain distinct from the classified interface member.")]
        public string Value => "visible";
    }

    [Fact]
    public void Runtime_Should_Not_Match_Analyzer_Name_Vectors()
    {
        var sanitizer = Policy();
        foreach (var name in new[] { "Response_StatusCode", "Secret", "CancellationToken", "Keyboard", "E_m_a_i_l", "ClientSecret", "Client_Assertion" })
            sanitizer.IsSensitive(name).Should().BeFalse();
        foreach (var name in new[] { "Sec", "ElapsedSeconds", "Section", "FlurlEx_ResponseCaptureLimited", "Client", "ClientId", "ClientErrorId", "Request_l5d-client-id" })
            sanitizer.IsSensitive(name).Should().BeFalse();
        sanitizer.IsSensitive("Émail").Should().BeFalse();
        sanitizer.IsSensitive("se.Cret").Should().BeFalse();
    }

    [Fact]
    public void Nullable_Classified_Property_Should_Not_Invoke_Its_Getter()
    {
        var data = JsonSerializer.SerializeToElement(Policy().SanitizeValue("Model", new NullableModel()));
        data.GetProperty("Value").GetString().Should().Be(DiagnosticSanitizer.Redacted);
    }

    [Sensitive] private readonly struct ClassifiedValue;
    private sealed class NullableModel
    {
        [SuppressMessage("Performance", "CA1822", Justification = "Instance getter verifies classified nullable properties are not read.")]
        public ClassifiedValue? Value => throw new InvalidOperationException("Must not execute");
    }

    [Fact]
    public void Property_Exemptions_Should_Preserve_Values_Without_Overriding_Classifications()
    {
        var sanitizer = Policy();
        var result = JsonSerializer.SerializeToElement(sanitizer.SanitizeValue("Model", new Model()));
        result.GetProperty("Keyboard").GetString().Should().Be("layout");
        result.GetProperty("Password").GetString().Should().Be(DiagnosticSanitizer.Redacted);
        result.GetProperty("Email").GetString().Should().Be(DiagnosticSanitizer.Redacted);
        result.GetProperty("IgnoredToken").GetString().Should().Be(ScopedLogConventions.IgnoredLogValue);
        result.GetProperty("Details").GetProperty("Password").GetString().Should().Be(DiagnosticSanitizer.Redacted);
        result.TryGetProperty("PublicField", out _).Should().BeFalse();
        Policy("keyboard").SanitizeValue("Model", new Model()).Should().NotBeNull();
        var configured = JsonSerializer.SerializeToElement(Policy("keyboard").SanitizeValue("Model", new Model()));
        configured.GetProperty("Keyboard").GetString().Should().Be("layout");
    }

    [Fact]
    public void Positive_Base_Classification_Should_Defeat_Override_False()
    {
        var data = JsonSerializer.SerializeToElement(Policy().SanitizeValue("Model", new DerivedModel()));
        data.GetProperty("Password").GetString().Should().Be(DiagnosticSanitizer.Redacted);
        data.GetProperty("Keyboard").GetString().Should().Be("override must remain protected");
    }

    [Fact]
    [SuppressMessage("DRN.Classification", "DRN0020", Justification = "Unclassified StatusCode and Password entries verify runtime policies do not infer classifications from names.")]
    public void Runtime_Policies_Should_Not_Trigger_Name_Matching_For_Unclassified_Values()
    {
        var sanitizer = Policy();
        var input = new Dictionary<string, object>
        {
            ["StatusCode"] = 503,
            ["Password"] = "secret-probe",
            ["Status.Code"] = "not-authorized"
        };
        var policy = new DiagnosticCapturePolicy(
            new(DiagnosticCaptureKind.NamedEntry, "Review this report container", "SecretReport"),
            new(DiagnosticCaptureKind.DictionaryKey, "HTTP status only", "SecretReport", "StatusCode"));
        var data = JsonSerializer.SerializeToElement(sanitizer.SanitizeValue("SecretReport", input, policy));
        data.GetProperty("StatusCode").GetInt32().Should().Be(503);
        data.GetProperty("Password").GetString().Should().Be("secret-probe");
        data.GetProperty("Status.Code").GetString().Should().Be("not-authorized");
        var wrongKind = new DiagnosticCapturePolicy(new DiagnosticNameExemption(DiagnosticCaptureKind.ObjectRoot, "Wrong kind", "SecretReport"));
        JsonSerializer.Serialize(Policy().SanitizeValue("SecretReport", input, wrongKind)).Should().Contain("secret-probe");
    }

    [Fact]
    [SuppressMessage("DRN.Classification", "DRN0017", Justification = "Unclassified Response_StatusCode replacement verifies native copies preserve the replacement value.")]
    public void Native_Copies_Should_Preserve_Values_Without_Destination_Name_Matching()
    {
        using var settings = SettingsProvider.Development();
        var source = new ScopedLog(settings, Policy());
        source.AddProperties("Model", new Model());
        source.AddWithPolicy("Response_StatusCode", 503,
            new DiagnosticCapturePolicy(new DiagnosticNameExemption(DiagnosticCaptureKind.NamedEntry, "HTTP status only", "Response_StatusCode")));
        var same = new ScopedLog(settings, Policy());
        same.CopyFrom(source);
        same.GetLogs()["Model.Keyboard"].Should().Be("layout");
        same.GetLogs()["Response_StatusCode"].Should().Be(503);
        var stricter = new ScopedLog(settings, Policy("keyboard", "statuscode"));
        stricter.CopyFrom(source);
        stricter.GetLogs()["Model.Keyboard"].Should().Be("layout");
        stricter.GetLogs()["Response_StatusCode"].Should().Be(503);
        source.Add("Response_StatusCode", "replacement");
        same.CopyFrom(source);
        same.GetLogs()["Response_StatusCode"].Should().Be("replacement");
        JsonSerializer.Serialize(Policy().SanitizeValue("", source.GetLogs())).Should().Contain("layout");
    }

    [Fact]
    public void Native_List_Copies_Should_Preserve_Values_And_Detach_Later_Appends()
    {
        using var settings = SettingsProvider.Development();
        var options = new DiagnosticLogOptions { SensitivePathSegments = ["handoff"], MaxProcessingMilliseconds = 100 };
        var source = new ScopedLog(settings, new DiagnosticSanitizer(options));
        source.AddToList("Responses", new CapturedResponse(503, "/handoff/private?opaque=visible&token=private"));
        var first = new ScopedLog(settings, new DiagnosticSanitizer(options));
        first.CopyFrom(source);
        var firstItems = JsonSerializer.SerializeToElement(first.GetLogs()["Responses"]);
        firstItems.GetArrayLength().Should().Be(1);
        firstItems[0].GetProperty("StatusCode").GetInt32().Should().Be(503);
        firstItems[0].GetProperty("CallbackUrl").GetString().Should().Be("/handoff/private?opaque=visible&token=private");

        source.AddToList("Responses", new CapturedResponse(504, "/?opaque=second"));
        var appended = new ScopedLog(settings, new DiagnosticSanitizer(options));
        appended.CopyFrom(source);
        var items = JsonSerializer.SerializeToElement(appended.GetLogs()["Responses"]);
        items.GetArrayLength().Should().Be(2);
        items[0].GetProperty("StatusCode").GetInt32().Should().Be(503);
        items[1].GetProperty("StatusCode").GetInt32().Should().Be(504);
        JsonSerializer.SerializeToElement(first.GetLogs()["Responses"]).GetArrayLength().Should().Be(1);

        foreach (var rule in new[] { "StatusCode", "opaque" })
        {
            var stricter = new ScopedLog(settings, Policy(rule));
            stricter.CopyFrom(appended);
            JsonSerializer.SerializeToElement(stricter.GetLogs()["Responses"])[0].GetProperty("StatusCode").GetInt32().Should().Be(503);
        }

        var urlRules = new DiagnosticSanitizer(new DiagnosticLogOptions
        {
            SensitivePathSegments = ["handoff", "exchange"], MaxProcessingMilliseconds = 100
        });
        var stricterUrls = new ScopedLog(settings, urlRules);
        stricterUrls.CopyFrom(appended);
        JsonSerializer.Serialize(stricterUrls.GetLogs()["Responses"]).Should().Contain("private");
    }

    [Fact]
    public void Appended_Classifications_Should_Survive_Detached_Copies_And_Reset_On_Replacement()
    {
        using var settings = SettingsProvider.Development();
        var source = new ScopedLog(settings, Policy());
        source.AddToList("Items", new ClassifiedListItem("first"));
        source.AddToList("Items", new ClassifiedListItem("second"));
        var copy = new ScopedLog(settings, Policy());
        copy.CopyFrom(source);

        source.AddToList("Items", new ClassifiedListItem("third"));
        var earlier = new ScopedLog(settings, Policy("LaterField"));
        earlier.CopyFrom(copy);
        JsonSerializer.SerializeToElement(earlier.GetLogs()["Items"]).GetArrayLength().Should().Be(2);
        var later = new ScopedLog(settings, Policy("LaterField"));
        later.CopyFrom(source);
        JsonSerializer.SerializeToElement(later.GetLogs()["Items"]).GetArrayLength().Should().Be(3);
        foreach (var item in JsonSerializer.SerializeToElement(later.GetLogs()["Items"]).EnumerateArray())
            item.GetProperty("Value").GetString().Should().Be(DiagnosticSanitizer.Redacted);
        JsonSerializer.SerializeToElement(earlier.GetLogs(false)["Items"]).EnumerateArray()
            .Select(item => item.GetProperty("Value").GetString()).Should().Equal("first", "second");
        JsonSerializer.SerializeToElement(later.GetLogs(false)["Items"])[2].GetProperty("Value").GetString().Should().Be("third");

        source.Add("Items", "replacement");
        source.AddToList("Items", "fresh");
        var replaced = new ScopedLog(settings, Policy("InitialField", "LaterField"));
        replaced.CopyFrom(source);
        JsonSerializer.SerializeToElement(replaced.GetLogs()["Items"])[0].GetString().Should().Be("fresh");
    }

    private sealed record CapturedResponse([property: Sensitive(false)] int StatusCode, string CallbackUrl);
    private sealed record ClassifiedListItem([property: Sensitive] string Value);

    [Fact]
    public void Native_Copies_Should_Not_Apply_Destination_Rules_To_Query_And_Fragment_Keys()
    {
        using var settings = SettingsProvider.Development();
        foreach (var (key, value) in new (string, object)[]
                 {
                     ("CallbackUrl", "/?%6fpaque=private"),
                     ("CallbackUri", "/#opaque=private"),
                     ("Request_QueryString", "opaque=private"),
                     ("Model", new { CallbackUrl = "/?opaque=private" })
                 })
        {
            var source = new ScopedLog(settings, Policy());
            source.Add(key, value);
            var same = new ScopedLog(settings, Policy());
            same.CopyFrom(source);
            JsonSerializer.Serialize(same.GetLogs()[key]).Should().Contain("private");

            foreach (var options in new[]
                     {
                         new DiagnosticLogOptions { SensitiveFields = ["opaque"], MaxProcessingMilliseconds = 100 },
                         new DiagnosticLogOptions { SensitiveFieldPaths = ["opaque"], MaxProcessingMilliseconds = 100 }
                     })
            {
                var stricter = new ScopedLog(settings, new DiagnosticSanitizer(options));
                stricter.CopyFrom(same);
                JsonSerializer.Serialize(stricter.GetLogs()[key]).Should().Contain("private");
            }
        }
    }

    [Fact]
    public void Flat_Base_Projection_Should_Respect_Runtime_Override_Classifications()
    {
        using var settings = SettingsProvider.Development();
        var log = new ScopedLog(settings, Policy());
        var model = new ClassifiedOverrideModel();
        log.AddProperties<ExemptBaseModel>("Model", model);
        log.GetLogs()["Model.Keyboard"].Should().Be(DiagnosticSanitizer.Redacted);
        model.ReadCount.Should().Be(1);
        log.GetLogs().Should().NotContainKey("Model.ReadCount");

        log.AddProperties<ExemptBaseModel>("Unchanged", new InheritedExemptionModel());
        log.GetLogs()["Unchanged.Keyboard"].Should().Be("layout");
        log.AddProperties<ExemptBaseModel>("Override", new UnclassifiedOverrideModel());
        log.GetLogs()["Override.Keyboard"].Should().Be("[diagnostic getter unavailable]");
    }

    private class ExemptBaseModel
    {
        [Sensitive(false)] public virtual string Keyboard => "layout";
    }

    [Fact]
    public void Interface_Projection_Should_Preserve_Implementation_Classification_After_Capture()
    {
        using var settings = SettingsProvider.Development();
        var log = new ScopedLog(settings, Policy());
        var implicitModel = new ImplicitClassifiedModel();
        var explicitModel = new ExplicitClassifiedModel();
        var inheritedModel = new InheritedClassifiedModel();

        log.AddProperties<IExemptProjection>("Implicit", implicitModel);
        log.AddProperties<IExemptProjection>("Explicit", explicitModel);
        log.AddProperties<IExemptProjection>("Inherited", inheritedModel);
        log.AddProperties<IExemptProjection>("Safe", new ExemptImplementation());

        var data = log.GetLogs();
        data["Implicit.Keyboard"].Should().Be(DiagnosticSanitizer.Redacted);
        data["Explicit.Keyboard"].Should().Be(DiagnosticSanitizer.Redacted);
        data["Inherited.Keyboard"].Should().Be(DiagnosticSanitizer.Redacted);
        data["Safe.Keyboard"].Should().Be("layout");
        data.Should().NotContainKey("Implicit.ReadCount").And.NotContainKey("Explicit.ReadCount");
        implicitModel.ReadCount.Should().Be(1);
        explicitModel.ReadCount.Should().Be(1);
        inheritedModel.ReadCount.Should().Be(1);
    }

    private interface IExemptProjection
    {
        [Sensitive(false)] string Keyboard { get; }
    }

    private class ImplicitClassifiedModel : IExemptProjection
    {
        public int ReadCount { get; private set; }
        [Sensitive] public string Keyboard
        {
            get { ReadCount++; return "private"; }
        }
    }

    private sealed class InheritedClassifiedModel : ImplicitClassifiedModel;

    private sealed class ExplicitClassifiedModel : IExemptProjection
    {
        public int ReadCount { get; private set; }
        [Personal] string IExemptProjection.Keyboard
        {
            get { ReadCount++; return "private"; }
        }
    }

    private sealed class ExemptImplementation : IExemptProjection
    {
        [Sensitive(false)] public string Keyboard => "layout";
    }

    [Theory]
    [DataInlineUnit("Name")]
    [DataInlineUnit("Outcome")]
    [DataInlineUnit("Reason")]
    public void Native_Event_Copies_Should_Preserve_Fields_Above_Destination_Field_Limits(string field)
    {
        using var settings = SettingsProvider.Development();
        var text = new string('x', 2048);
        var original = new ScopeEvent(new EventId(42, field == "Name" ? text : "Save"),
            field == "Outcome" ? text : null, field == "Reason" ? text : null);
        var source = new ScopedLog(settings, Policy());
        source.Add("Report", original);
        var same = new ScopedLog(settings, Policy());
        same.CopyFrom(source);
        same.GetLogs()["Report"].Should().Be(original);

        var sanitizer = new DiagnosticSanitizer(new DiagnosticLogOptions
        {
            MaxFieldBytes = 1024,
            MaxProcessingMilliseconds = 100
        });
        var destination = new ScopedLog(settings, sanitizer);
        destination.CopyFrom(source);

        destination.GetLogs()["Report"].Should().BeSameAs(original);
        sanitizer.GetStatus().Should().NotContainKey("DiagnosticCaptureWarnings");
        source.GetLogs()["Report"].Should().Be(original);
    }

    [Fact]
    public void Native_Event_Copies_Should_Not_Charge_Strings_To_Destination_Input_Budget()
    {
        using var settings = SettingsProvider.Development();
        var text = new string('x', 600);
        var original = new ScopeEvent(new EventId(42, text), text, text);
        var source = new ScopedLog(settings, Policy());
        source.Add("Report", original);
        var sanitizer = new DiagnosticSanitizer(new DiagnosticLogOptions
        {
            MaxFieldBytes = 1024,
            MaxInputBytes = 1024,
            MaxProcessingMilliseconds = 100
        });
        var destination = new ScopedLog(settings, sanitizer);
        destination.CopyFrom(source);

        destination.GetLogs()["Report"].Should().BeSameAs(original);
        sanitizer.GetStatus().Should().NotContainKey("DiagnosticCaptureWarnings");
        source.GetLogs()["Report"].Should().Be(original);
    }

    private sealed class ClassifiedOverrideModel : ExemptBaseModel
    {
        public int ReadCount { get; private set; }
        [Sensitive] public override string Keyboard
        {
            get { ReadCount++; return "private"; }
        }
    }

    private sealed class InheritedExemptionModel : ExemptBaseModel;
    private sealed class UnclassifiedOverrideModel : ExemptBaseModel
    {
        [SuppressMessage("DRN.Classification", "DRN0015", Justification = "Keep the override unannotated to test inherited exemption handling and getter failure.")]
        public override string Keyboard => throw new InvalidOperationException("Must not execute");
    }

    [Fact]
    public void Typed_Properties_Should_Ignore_BuiltIn_Prefix_Matches_And_Keep_Getter_Protection()
    {
        using var settings = SettingsProvider.Development();
        var log = new ScopedLog(settings, Policy());
        log.AddProperties("ClientError", new Model());
        log.GetLogs()["ClientError.Keyboard"].Should().Be("layout");
        log.GetLogs()["ClientError.Password"].Should().Be(DiagnosticSanitizer.Redacted);
        JsonSerializer.SerializeToElement(log.GetLogs()["ClientError.Details"])
            .GetProperty("Password").GetString().Should().Be(DiagnosticSanitizer.Redacted);

        var nested = JsonSerializer.SerializeToElement(Policy().SanitizeValue("ClientError", new Model()));
        nested.GetProperty("Keyboard").GetString().Should().Be("layout");
        nested.GetProperty("Password").GetString().Should().Be(DiagnosticSanitizer.Redacted);
        nested.GetProperty("Details").GetProperty("Password").GetString().Should().Be(DiagnosticSanitizer.Redacted);
    }

    [Fact]
    public void Typed_Capture_And_Native_Copies_Should_Ignore_Configured_Full_Paths()
    {
        using var settings = SettingsProvider.Development();
        var source = new ScopedLog(settings, Policy());
        source.AddProperties("ClientError", new Model());
        source.Add("ClientReport", new Model());
        var same = new ScopedLog(settings, Policy());
        same.CopyFrom(source);
        same.GetLogs()["ClientError.Keyboard"].Should().Be("layout");
        JsonSerializer.SerializeToElement(same.GetLogs()["ClientReport"]).GetProperty("Keyboard").GetString().Should().Be("layout");

        var options = new DiagnosticLogOptions
        {
            SensitiveFieldPaths = ["ClientError.Keyboard", "ClientReport.Keyboard"],
            MaxProcessingMilliseconds = 100
        };
        var stricter = new ScopedLog(settings, new DiagnosticSanitizer(options));
        stricter.CopyFrom(source);
        stricter.GetLogs()["ClientError.Keyboard"].Should().Be("layout");
        JsonSerializer.SerializeToElement(stricter.GetLogs()["ClientReport"]).GetProperty("Keyboard").GetString().Should().Be("layout");
        var direct = JsonSerializer.SerializeToElement(new DiagnosticSanitizer(options).SanitizeValue("ClientReport", new Model()));
        direct.GetProperty("Keyboard").GetString().Should().Be("layout");
        JsonSerializer.SerializeToElement(Policy("ClientReport").SanitizeValue("ClientReport", new Model()))
            .GetProperty("Keyboard").GetString().Should().Be("layout");
    }

    [Fact]
    public void Typed_Collection_Properties_Should_Preserve_Unclassified_Children()
    {
        var model = new CollectionModel();
        var data = JsonSerializer.SerializeToElement(Policy().SanitizeValue("ClientReport", model), ProjectionJsonOptions);
        data.GetProperty("Values")[0].GetString().Should().Be("public");
        data.GetProperty("Metadata").GetProperty("Count").GetInt32().Should().Be(3);
        data.GetProperty("Metadata").GetProperty("Password").GetString().Should().Be("private");
        data.GetProperty("Json").GetProperty("ValueKind").GetInt32().Should().Be((int)JsonValueKind.Object);
        data.GetProperty("Json").TryGetProperty("Count", out _).Should().BeFalse();
    }

    private sealed class CollectionModel
    {
        public string[] Values { get; } = ["public"];
        public Dictionary<string, object> Metadata { get; } = new() { ["Count"] = 3, ["Password"] = "private" };
        [SuppressMessage("DRN.Classification", "DRN0019", Justification = "Unclassified anonymous JSON fixture verifies capture projects JsonElement properties instead of its payload.")]
        public JsonElement Json { get; } = JsonSerializer.SerializeToElement(new { Count = 3, Password = "private" },
            FixtureJsonOptions);
    }

    [Fact]
    [SuppressMessage("Performance", "CA1859", Justification = "Exercise interface extension dispatch to the native implementations.")]
    [SuppressMessage("DRN.Classification", "DRN0017", Justification = "Unclassified SecretReport capture is the control for the complex-value reason overload rejection.")]
    [SuppressMessage("DRN.Classification", "DRN0019", Justification = "Anonymous Password fixture verifies runtime fallback before reason overload rejection.")]
    public void Reason_Overloads_Should_Exempt_Only_The_Current_Node_And_Preserve_Other_Protection()
    {
        using var settings = SettingsProvider.Development();
        IScopedLog log = new ScopedLog(settings, Policy());
        log.Add("Response_StatusCode", 503, exemptionReason: "HTTP response status.");
        log.GetLogs()["Response_StatusCode"].Should().Be(503);
        log.Add("TokenDuration", TimeSpan.FromSeconds(2), exemptionReason: "Rate limit timing.");
        log.GetLogs()["TokenDuration_Seconds"].Should().Be(2d);
        log.Add("InvalidReason", "private", exemptionReason: " ");
        log.GetLogs()["InvalidReason"].Should().Be("[diagnostic field unavailable]");
        log.Add("NullReason", "private", exemptionReason: null!);
        log.GetLogs()["NullReason"].Should().Be("[diagnostic field unavailable]");
        var copy = new ScopedLog(settings, Policy());
        copy.CopyFrom(log);
        copy.GetLogs()["Response_StatusCode"].Should().Be(503);
        var stricter = new ScopedLog(settings, Policy("StatusCode"));
        stricter.CopyFrom(log);
        stricter.GetLogs()["Response_StatusCode"].Should().Be(503);

        IDiagnosticSanitizer sanitizer = Policy();
        var anonymous = new { Count = 3, Password = "private" };
        JsonSerializer.SerializeToElement(sanitizer.SanitizeValue("SecretReport", anonymous)).GetProperty("Password").GetString().Should().Be("private");
        sanitizer.SanitizeValue("SecretReport", anonymous, exemptionReason: "Inspect report root.")
            .Should().Be("[diagnostic field unavailable]");
        Policy("StatusCode").SanitizeValue("StatusCode", 503, "HTTP status.").Should().Be(503);
        Policy().SanitizeValue("Target", new ClassifiedUri(), "Reviewed entry.").Should().Be(DiagnosticSanitizer.Redacted);
        Policy().SanitizeValue("CallbackUrl", "/?token=private", "Callback URL.").Should().Be("/?token=private");
        Policy().SanitizeValue("StatusCode", 503, " ").Should().Be("[diagnostic field unavailable]");
        var bounded = new DiagnosticSanitizer(new DiagnosticLogOptions { MaxFieldBytes = 1024, MaxProcessingMilliseconds = 100 });
        bounded.SanitizeValue("StatusCode", new string('x', 2048), "Reviewed entry.").Should().Be(DiagnosticSanitizer.Limited);

        var foreign = Substitute.For<IScopedLog>();
        foreign.Add("StatusCode", 503, exemptionReason: "HTTP status.");
        foreign.Received(1).Add("StatusCode", 503);
    }

    [Fact]
    [SuppressMessage("Performance", "CA1859", Justification = "Verify nullable values through the interface extension overload.")]
    public void Simple_Exemptions_Should_Accept_Supported_Scalars_And_Nullable_Values()
    {
        object?[] values = [null, "public", true, (byte)1, (sbyte)-1, (short)-2, (ushort)2,
            3, 3u, 4L, 4UL, 1.25f, 1.25d, 1.25m, Guid.Empty,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), DateTimeOffset.UnixEpoch,
            new DateOnly(2026, 1, 1), new TimeOnly(12, 30), TimeSpan.FromSeconds(2), DayOfWeek.Monday,
            (int?)7, (DateOnly?)new DateOnly(2026, 2, 1), (int?)null];
        foreach (var value in values)
            Policy().SanitizeValue("StatusCode", value, "Reviewed scalar.").Should().Be(value);
        Policy().SanitizeValue("StatusCode", 'A', "Reviewed scalar.").Should().Be("A");
        Policy().SanitizeValue("StatusCode", double.NaN, "Reviewed scalar.").Should().Be("[diagnostic field unavailable]");
        Policy().SanitizeValue("StatusCode", float.PositiveInfinity, "Reviewed scalar.").Should().Be("[diagnostic field unavailable]");

        using var settings = SettingsProvider.Development();
        IScopedLog log = new ScopedLog(settings, Policy());
        log.Add("StatusCode", null, exemptionReason: "No status available.");
        log.GetLogs()["StatusCode"].Should().Be(string.Empty);
        var date = new DateOnly(2026, 1, 1);
        var time = new TimeOnly(12, 30);
        log.Add("Date", date);
        log.Add("Time", time);
        JsonSerializer.SerializeToElement(log.GetLogs()).GetProperty("Date").GetString().Should().Be("2026-01-01");
        JsonSerializer.SerializeToElement(log.GetLogs()).GetProperty("Time").GetString().Should().Be("12:30:00");
    }

    [Fact]
    public void Simple_Exemptions_Should_Reject_Complex_Values_Without_Traversing_Them()
    {
        object[] values = [new Model(), new { Count = 3 }, new Dictionary<string, object> { ["Count"] = 3 },
            new[] { "public" }, new List<int> { 1 }, Enumerable.Range(1, 3), new ClassifiedValue(),
            JsonSerializer.SerializeToElement(3), new ScopeEvent(new EventId(1, "Save"))];
        using var settings = SettingsProvider.Development();
        foreach (var value in values)
        {
            var sanitizer = Policy();
            sanitizer.SanitizeValue("Report", value, "Reviewed value.").Should().Be("[diagnostic field unavailable]");
            var log = new ScopedLog(settings, Policy());
            log.Add("Report", value, exemptionReason: "Reviewed value.");
            log.GetLogs()["Report"].Should().Be("[diagnostic field unavailable]");
        }
        var foreign = Substitute.For<IScopedLog>();
        foreign.Add("Report", new Model(), exemptionReason: "Reviewed value.");
        foreign.Received(1).Add("Report", "[diagnostic field unavailable]");
        var foreignSanitizer = Substitute.For<IDiagnosticSanitizer>();
        foreignSanitizer.SanitizeValue("Report", new Model(), exemptionReason: "Reviewed value.");
        foreignSanitizer.Received(1).SanitizeValue("Report", "[diagnostic field unavailable]");
    }

    [Fact]
    [SuppressMessage("DRN.Classification", "DRN0017", Justification = "Unclassified SecretTarget verifies URI credential removal independently of name matching.")]
    public void Uri_Scalars_Should_Remove_Credentials_Without_Matching_Names_In_Copies()
    {
        var uri = new Uri("https://user:private@example.test/resetpassword/private?token=private&opaque=visible");
        const string expected = "https://example.test/resetpassword/private?token=private&opaque=visible";
        Policy().SanitizeValue("Target", uri).Should().Be(expected);
        Policy().SanitizeValue("SecretTarget", uri).Should().Be(expected);
        Policy().SanitizeValue("SecretTarget", uri, "Reviewed target.").Should().Be(expected);
        Policy().SanitizeValue("Target", new Uri("/resetpassword/private", UriKind.Relative), "Reviewed target.")
            .Should().Be("/resetpassword/private");

        using var settings = SettingsProvider.Development();
        var source = new ScopedLog(settings, Policy());
        source.Add("SecretTarget", uri, exemptionReason: "Reviewed target.");
        var same = new ScopedLog(settings, Policy());
        same.CopyFrom(source);
        same.GetLogs()["SecretTarget"].Should().Be(expected);
        var stricter = new ScopedLog(settings, Policy("opaque"));
        stricter.CopyFrom(source);
        stricter.GetLogs()["SecretTarget"].Should().Be(expected);
        var bounded = new DiagnosticSanitizer(new DiagnosticLogOptions { MaxFieldBytes = 1024, MaxProcessingMilliseconds = 100 });
        bounded.SanitizeValue("Target", new Uri("https://example.test/" + new string('x', 2048)), "Reviewed target.")
            .Should().Be(DiagnosticSanitizer.Limited);
    }

    [Sensitive]
    private sealed class ClassifiedUri() : Uri("https://example.test/");

    [Fact]
    public void Invalid_Derived_And_Type_Exemptions_Should_Fail_Closed()
    {
        Action construct = () => _ = new InvalidAttribute();
        construct.Should().Throw<ArgumentException>();
        Policy().SanitizeValue("Model", new InvalidModel()).Should().Be("[diagnostic field unavailable]");
        Policy().SanitizeValue("Model", new InvalidType()).Should().Be(DiagnosticSanitizer.Redacted);
    }

    private class CompanyAttribute : SensitiveAttribute;
    private sealed class ContactAttribute : PersonalAttribute;
    [SuppressMessage("DRN.Classification", "DRN0016", Justification = "Invalid derived attribute constructor deliberately passes false to verify runtime rejection.")]
    private sealed class InvalidAttribute() : SensitiveAttribute(false);
    private sealed class InvalidModel
    {
        [SuppressMessage("Performance", "CA1822", Justification = "Instance getter exercises invalid property classification.")]
        [Invalid] public string Value => throw new InvalidOperationException("Must not execute");
    }
    [SuppressMessage("DRN.Classification", "DRN0016", Justification = "Invalid type-level exemption verifies runtime capture fails closed.")]
    [Sensitive(false)] private sealed class InvalidType
    {
        [SuppressMessage("Performance", "CA1822", Justification = "Instance getter exercises invalid type classification.")]
        public string Value => throw new InvalidOperationException("Must not execute");
    }
    private class BaseModel
    {
        [Company] public virtual string Password => throw new InvalidOperationException("Must not execute");
        [Sensitive(false)] public virtual string Keyboard => "base";
    }
    private sealed class DerivedModel : BaseModel
    {
        [Sensitive(false)] public override string Password => throw new InvalidOperationException("Must not execute");
        [SuppressMessage("DRN.Classification", "DRN0015", Justification = "Keep the override unannotated to verify runtime inheritance of the base exemption.")]
        public override string Keyboard => "override must remain protected";
    }
    private sealed class Model
    {
        [SuppressMessage("Performance", "CA1822", Justification = "Instance property exercises classification exemptions during capture.")]
        [Sensitive(false)] public string Keyboard => "layout";
        [SuppressMessage("Performance", "CA1822", Justification = "Instance getter verifies positive classification overrides an exemption.")]
        [Company, Sensitive(false)] public string Password => throw new InvalidOperationException("Must not execute");
        [SuppressMessage("Performance", "CA1822", Justification = "Instance getter verifies personal classification prevents reads.")]
        [Contact] public string Email => throw new InvalidOperationException("Must not execute");
        [SuppressMessage("Performance", "CA1822", Justification = "Instance getter verifies IgnoreLog prevents reads.")]
        [SuppressMessage("DRN.Classification", "DRN0015", Justification = "Unclassified ignored token verifies IgnoreLog prevents getter execution independently of classification.")]
        [IgnoreLog] public string IgnoredToken => throw new InvalidOperationException("Must not execute");
        [SuppressMessage("Performance", "CA1822", Justification = "Instance property exercises nested model capture.")]
        public Detail Details => new();
        public string PublicField = "omitted";
    }
    private sealed class Detail
    {
        [SuppressMessage("Performance", "CA1822", Justification = "Instance getter verifies nested classification prevents reads.")]
        [Sensitive] public string Password => throw new InvalidOperationException("Classified getter");
    }
}
