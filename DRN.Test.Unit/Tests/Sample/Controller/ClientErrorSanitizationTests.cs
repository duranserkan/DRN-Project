using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Logging.Diagnostics;
using DRN.Framework.Utils.Scope;
using Sample.Hosted.Controllers.Sample;
using Microsoft.AspNetCore.Mvc;

namespace DRN.Test.Unit.Tests.Sample.Controller;

public class ClientErrorSanitizationTests
{
    [Theory]
    [DataInlineUnit]
    public void Typed_Reports_Should_Preserve_Text_And_Redact_Urls_Without_Relying_On_Browser_Policy(DrnTestContextUnit services)
    {
        var user = Substitute.For<IScopedUser>();
        user.Id.Returns("user-id");
        var log = new ScopedLog(services.GetRequiredService<IAppSettings>());
        ScopeContext.InitializeForTest(services, scopedLog: log, scopedUser: user);
        var controller = new ClientErrorController(log, log.Sanitizer);
        var payload = new ClientErrorPayload
        {
            ClientErrorId = new string('a', 32),
            Message = "Save failed password=message-probe; RecoveryCodes=[\"first-probe\",\"second-probe\"]; retry",
            Stack = "Service.Save\nAuthorization: Bearer stack-probe", Url = "https://example.test/?token=url-probe",
            DiagnosticLimited = true
        };

        controller.Report(payload).Should().BeOfType<OkResult>();

        log.ToString().Should().NotContain("url-probe");
        log.GetLogs()["ClientError.Message"].Should().Be(payload.Message);
        log.GetLogs()["ClientError.ClientErrorId"].Should().Be(payload.ClientErrorId);
        log.GetLogs()["ClientError.DiagnosticLimited"].Should().Be(true);
        log.GetLogs()["ClientError.Stack"].Should().Be(payload.Stack);
        log.GetLogs()["ClientError.Url"].Should().Be("https://example.test/?token=redacted");
        payload.Message.Should().Contain("message-probe");
    }
}
