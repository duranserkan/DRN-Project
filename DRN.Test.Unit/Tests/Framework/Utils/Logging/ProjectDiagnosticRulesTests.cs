using ValidationException = System.ComponentModel.DataAnnotations.ValidationException;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.SharedKernel.Diagnostics;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Logging.Diagnostics;
using DRN.Test.Shared;
using static DRN.Test.Unit.Tests.Framework.Utils.Logging.DiagnosticTestSupport;

namespace DRN.Test.Unit.Tests.Framework.Utils.Logging;

public class ProjectDiagnosticRulesTests
{
    private static readonly int[] CancellationReferences = [1];

    [Fact]
    public void Missing_Transitive_Assemblies_Should_Not_Discard_Available_Catalogs()
    {
        var available = typeof(ProjectDiagnosticRulesTests).Assembly;
        var references = new[] { new AssemblyName("Missing.Optional.Dependency"), available.GetName() };
        var loaded = EmbeddedClassificationRules.LoadReferences(references, reference =>
            reference.Name == available.GetName().Name ? available : throw new FileNotFoundException()).ToArray();
        loaded.Should().ContainSingle().Which.Should().BeSameAs(available);
        var rules = EmbeddedClassificationRules.Load(loaded[0]);
        rules.MatchesField("FixtureMemberReference").Should().BeTrue();
        rules.MatchesField("Fixture3BReference").Should().BeTrue();

        Action missingRoot = () => EmbeddedClassificationRules.Load("Missing.Classification.Root." + Guid.NewGuid().ToString("N"));
        missingRoot.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Invalid_Transitive_Assemblies_Should_Still_Fail_Discovery()
    {
        var references = new[] { new AssemblyName("Invalid.Dependency") };
        Action failedLoad = () => _ = EmbeddedClassificationRules.LoadReferences(references,
            _ => throw new FileLoadException()).ToArray();
        failedLoad.Should().Throw<FileLoadException>();
        Action invalidImage = () => _ = EmbeddedClassificationRules.LoadReferences(references,
            _ => throw new BadImageFormatException()).ToArray();
        invalidImage.Should().Throw<BadImageFormatException>();
    }

    [Fact]
    public void Inheritance_Should_Handle_Cycles_And_Ignore_Unrelated_Invalid_Catalogs()
    {
        var kernel = new CatalogAssembly("DRN.Framework.SharedKernel");
        var root = new CatalogAssembly("App", "{\"secretFields\":[\"graphOwn\"]}");
        var dependency = new CatalogAssembly("Dependency", "{\"personalFields\":[\"graphMember\"]}");
        root.References.AddRange([dependency, new CatalogAssembly("Unrelated", "{")]);
        dependency.References.AddRange([root, kernel]);
        var rules = DiagnosticRedactionRules.FromGraph<CatalogAssembly>(root, item => item.Name, item => item.References,
            item => item.Catalog == null ? [] : [item.Catalog], EqualityComparer<CatalogAssembly>.Default);
        rules.MatchesField("GraphOwn").Should().BeTrue();
        rules.MatchesField("GraphMember").Should().BeTrue();
        rules.MatchesField("Password").Should().BeTrue();
        rules.MatchesField("Other").Should().BeFalse();
    }

    [Fact]
    public void Inherited_Rule_Merges_Should_Be_Deduplicated_And_Bounded()
    {
        var rules = DiagnosticRedactionRules.Empty;
        for (var group = 0; group < 16; group++)
        {
            var json = "{\"personalFields\":[" + string.Join(",", Enumerable.Range(0, 256)
                .Select(index => "\"field" + group + "item" + index + "\"")) + "]}";
            var addition = DiagnosticRedactionRules.Parse(json);
            rules = rules.Merge(addition).Merge(addition);
        }
        rules.PersonalFields.Should().HaveCount(4096);
        Action overflow = () => rules.Merge(DiagnosticRedactionRules.Parse("{\"personalFields\":[\"overflow\"]}"));
        overflow.Should().Throw<FormatException>();
    }

    private sealed class CatalogAssembly(string name, string? catalog = null)
    {
        internal string Name { get; } = name;
        internal string? Catalog { get; } = catalog;
        internal List<CatalogAssembly> References { get; } = [];
    }

    [Fact]
    public void Graph_Should_Accept_1024_Assemblies_And_Reject_The_Next()
    {
        var root = new CatalogAssembly("DRN.Framework.SharedKernel");
        root.References.AddRange(Enumerable.Range(0, 1023).Select(index => new CatalogAssembly("Dependency" + index)));
        DiagnosticRedactionRules Read() => DiagnosticRedactionRules.FromGraph<CatalogAssembly>(root,
            item => item.Name, item => item.References, _ => [], EqualityComparer<CatalogAssembly>.Default);

        Read().SecretFields.Should().BeEmpty();
        root.References.Add(new CatalogAssembly("Overflow"));
        Action overflow = () => Read();
        overflow.Should().Throw<FormatException>()
            .WithMessage("Classification inheritance supports at most 1024 assemblies.");
    }

    [Fact]
    public void Graph_Should_Bound_References_Before_Deduplicating_Assemblies()
    {
        var root = new CatalogAssembly("App");
        var kernel = new CatalogAssembly("DRN.Framework.SharedKernel");
        root.References.AddRange(Enumerable.Repeat(kernel, 1024));
        DiagnosticRedactionRules Read() => DiagnosticRedactionRules.FromGraph<CatalogAssembly>(root,
            item => item.Name, item => item.References, _ => [], EqualityComparer<CatalogAssembly>.Default);

        Read().SecretFields.Should().BeEmpty();
        root.References.Add(kernel);
        Action overflow = () => Read();
        overflow.Should().Throw<FormatException>()
            .WithMessage("Classification inheritance supports at most 1024 assembly references.");
    }

    [Fact]
    public void Graph_Should_Count_Catalogs_Even_When_Their_Rules_Are_Identical()
    {
        var root = new CatalogAssembly("DRN.Framework.SharedKernel");
        for (var index = 0; index < 65; index++)
        {
            var consumer = new CatalogAssembly("Consumer" + index, """{"secretFields":["catalogProbe"]}""");
            consumer.References.Add(root);
            root.References.Add(consumer);
        }
        var extra = root.References[^1];
        root.References.RemoveAt(64);
        DiagnosticRedactionRules Read() => DiagnosticRedactionRules.FromGraph<CatalogAssembly>(root,
            item => item.Name, item => item.References,
            item => item.Catalog == null ? [] : [item.Catalog], EqualityComparer<CatalogAssembly>.Default);

        Read().SecretFields.Should().Equal("catalogprobe");
        root.References.Add(extra);
        Action overflow = () => Read();
        overflow.Should().Throw<FormatException>()
            .WithMessage("Classification inheritance supports at most 64 catalogs.");
    }

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public void Graph_Should_Observe_Cancellation_Before_And_During_Discovery(bool duringDiscovery)
    {
        using var cancellation = new CancellationTokenSource();
        if (!duringDiscovery) cancellation.Cancel();
        var referenceCalls = 0;
        var catalogCalls = 0;
        Action read = () => DiagnosticRedactionRules.FromGraph(0, _ => "DRN.Framework.SharedKernel",
            _ =>
            {
                referenceCalls++;
                cancellation.Cancel();
                return CancellationReferences;
            },
            _ =>
            {
                catalogCalls++;
                return Array.Empty<string?>();
            }, EqualityComparer<int>.Default, cancellation.Token);

        read.Should().Throw<OperationCanceledException>().Which.CancellationToken.Should().Be(cancellation.Token);
        referenceCalls.Should().Be(duringDiscovery ? 1 : 0);
        catalogCalls.Should().Be(0);
    }

    [Fact]
    public void Runtime_Should_Not_Apply_Embedded_Catalog_Names()
    {
        var sanitizer = new DiagnosticSanitizer(new DiagnosticLogOptions
        {
            RuleAssemblyName = typeof(ProjectDiagnosticRulesTests).Assembly.GetName().Name,
            MaxProcessingMilliseconds = 100
        });
        // These names come only from the referenced test-support project's embedded catalog.
        foreach (var field in new[] { "FixtureMemberReference", "Fixture3BReference" })
            sanitizer.SanitizeValue(field, "probe").Should().Be("probe");
        sanitizer.SanitizeValue("UnrelatedReference", "probe").Should().Be("probe");
    }

    private static DiagnosticLogOptions Options() => new()
    {
        RuleFile = "DiagnosticRules/data-classification-rules.json", MaxProcessingMilliseconds = 100
    };

    [Fact]
    public void Project_File_Should_Not_Cause_Runtime_Name_Redaction()
    {
        var sanitizer = new DiagnosticSanitizer(Options());
        foreach (var name in new[] { "Challenge_Response", "MEMBERSHIP_NUMBER", "Payload.PrivateValue", "Password" })
            sanitizer.SanitizeValue(name, "probe").Should().Be("probe");
        sanitizer.SanitizeValue("FileName", "report.txt").Should().Be("report.txt");
        sanitizer.SanitizeUrl("/handoff/private?membership_number=member&attempt=2")
            .Should().Be("/handoff/private?membership_number=member&attempt=2");
        sanitizer.SanitizeUrl("/token/private").Should().Be("/token/private");
        sanitizer.SanitizeBody("membershipNumber=member", "application/x-www-form-urlencoded")
            .Should().Be("membershipNumber=member");
    }

    [Fact]
    public void Settings_Loading_Should_Use_The_Same_Project_File()
    {
        using var settings = SettingsProvider.Development(new { DiagnosticLogging = Options() });
        var sanitizer = DiagnosticSanitizer.FromSettings(settings);
        sanitizer.SanitizeValue("MembershipNumber", "member").Should().Be("member");
        var log = new ScopedLog(settings);
        log.Add("MembershipNumber", "member");
        log.GetLogs()["MembershipNumber"].Should().Be("member");
    }

    [Fact]
    public void Settings_Loading_Should_Reject_Invalid_Configured_Catalogs()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{\"personalFields\":null}");
            using var settings = SettingsProvider.Development(new { DiagnosticLogging = Options() with { RuleFile = path } });
            Action sanitizer = () => DiagnosticSanitizer.FromSettings(settings);
            Action log = () => _ = new ScopedLog(settings);
            sanitizer.Should().Throw<FormatException>();
            log.Should().Throw<FormatException>();
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Project_Rules_Should_Preserve_Reviewed_Values_Without_Runtime_Matching()
    {
        var sanitizer = new DiagnosticSanitizer(Options());
        var value = (IReadOnlyDictionary<string, object>)sanitizer.SanitizeValue("Model", new ReviewedModel())!;
        value["MembershipNumber"].Should().Be("public-reference");
        var configured = new DiagnosticSanitizer(Options() with { SensitiveFields = ["membershipnumber"] });
        var protectedValue = (IReadOnlyDictionary<string, object>)configured.SanitizeValue("Model", new ReviewedModel())!;
        protectedValue["MembershipNumber"].Should().Be("public-reference");
    }

    private sealed class ReviewedModel
    {
        [SuppressMessage("Performance", "CA1822", Justification = "Instance property exercises reflection capture of a reviewed value.")]
        [Sensitive(false, Justification = "Public reference in this fixture.")]
        public string MembershipNumber => "public-reference";
    }

    [Fact]
    public void Project_Field_Paths_Should_Not_Redact_Typed_Models_At_Runtime()
    {
        var sanitizer = new DiagnosticSanitizer(Options());
        var value = (IReadOnlyDictionary<string, object>)sanitizer.SanitizeValue("Payload", new PathModel())!;
        value["PrivateValue"].Should().Be("probe");
        var other = (IReadOnlyDictionary<string, object>)sanitizer.SanitizeValue("Other", new PathModel())!;
        other["PrivateValue"].Should().Be("probe");
    }

    private sealed class PathModel
    {
        [SuppressMessage("Performance", "CA1822", Justification = "Instance property exercises typed model capture with configured paths.")]
        [Sensitive(false, Justification = "Exercise full-path protection independently of name exemptions.")]
        public string PrivateValue => "probe";
    }

    [Fact]
    public void Rule_Validation_Should_Be_Cached_Per_Options_And_Snapshots_Should_Survive_File_Removal()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{\"personalFields\":[\"membershipNumber\"]}");
            var options = Options() with { RuleFile = path };
            using var settings = SettingsProvider.Development();
            var log = new ScopedLog(settings, new DiagnosticSanitizer(options));
            log.Add("MembershipNumber", "member");
            File.WriteAllText(path, "{\"personalFields\":null}");
            new DiagnosticSanitizer(options).SanitizeValue("MembershipNumber", "member").Should().Be("member");
            Action invalid = () => _ = new DiagnosticSanitizer(options with { });
            invalid.Should().Throw<FormatException>();
            File.Delete(path);
            log.GetLogs()["MembershipNumber"].Should().Be("member");
            var copy = new ScopedLog(settings, Policy());
            copy.CopyFrom(log);
            copy.GetLogs()["MembershipNumber"].Should().Be("member");
            Action missing = () => _ = new DiagnosticSanitizer(options with { });
            missing.Should().Throw<FileNotFoundException>();
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Changing_A_Snapshot_RuleFile_Should_Validate_The_New_File()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{\"personalFields\":null}");
            var original = new DiagnosticSanitizer(Options());
            Action changed = () => _ = new DiagnosticSanitizer(original.Options with { RuleFile = path });
            changed.Should().Throw<FormatException>();
            original.SanitizeValue("MembershipNumber", "member").Should().Be("member");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Copies_Should_Preserve_Urls_Without_Applying_Project_Name_Rules()
    {
        using var settings = SettingsProvider.Development();
        var source = new ScopedLog(settings, new DiagnosticSanitizer(Options()));
        source.Add("TargetUrl", "/handoff/private?membershipNumber=member");
        var destination = new ScopedLog(settings, Policy());
        destination.CopyFrom(source);
        destination.GetLogs()["TargetUrl"].Should().Be("/handoff/private?membershipNumber=member");
    }

    [Fact]
    public void Identically_Configured_Copies_Should_Preserve_Metrics_And_Url_Values()
    {
        using var settings = SettingsProvider.Development();
        var source = new ScopedLog(settings, new DiagnosticSanitizer(Options()));
        source.Increase("Completed", 3);
        source.IncreaseTimeSpentOn("Work", TimeSpan.FromSeconds(2));
        source.Add("TargetUrl", "/handoff/private");
        var destination = new ScopedLog(settings, new DiagnosticSanitizer(Options()));

        destination.CopyFrom(source);

        foreach (var log in new[] { source, destination })
        {
            var snapshot = log.GetLogs();
            snapshot["Stats_Completed"].Should().Be(3L);
            snapshot["Stats_TimeSpentOn_Work_Counter"].Should().Be(1L);
            snapshot["Stats_TimeSpentOn_Work_Seconds"].Should().Be(2d);
            snapshot["TargetUrl"].Should().Be("/handoff/private");
        }
    }

    [Fact]
    public void Legacy_Project_File_Configuration_Should_Still_Be_Validated()
    {
        var path = Path.GetTempFileName();
        try
        {
            foreach (var json in InvalidDiagnosticRuleCases.Create())
            {
                File.WriteAllText(path, json);
                Action create = () => _ = new DiagnosticSanitizer(Options() with { RuleFile = path });
                create.Should().Throw<FormatException>();
            }
            Action blank = () => _ = new DiagnosticSanitizer(Options() with { RuleFile = " " });
            blank.Should().Throw<ValidationException>();
        }
        finally { File.Delete(path); }
    }
}
