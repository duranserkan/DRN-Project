using System.Text.Json;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Logging.Diagnostics;

namespace DRN.Test.Unit.Tests.Framework.Utils.Logging;

public class DiagnosticCommentExemptionTests
{
    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public void Dictionary_Comments_And_Configured_Paths_Should_Not_Redact_Unclassified_Values(bool configured)
    {
        using var settings = SettingsProvider.Development();
        var sanitizer = new DiagnosticSanitizer(new DiagnosticLogOptions
        {
            SensitiveFieldPaths = configured ? ["Data.Public"] : [],
            MaxProcessingMilliseconds = 100
        });
        var log = new ScopedLog(settings, sanitizer);
        var data = new Dictionary<string, object> { ["Password"] = "credential-probe", ["Public"] = "public-probe" };

        // DiagnosticNameExemption: Password | Deliberately capture an unclassified fixture.
        log.Add("Data", data);
        // DiagnosticNameExemption: Password | Deliberately capture an unclassified fixture.
        var direct = sanitizer.SanitizeValue("Data", data);

        foreach (var snapshot in new[] { JsonSerializer.Serialize(log.GetLogs()), JsonSerializer.Serialize(direct) })
        {
            snapshot.Should().Contain("credential-probe").And.Contain("public-probe");
        }
    }
}
