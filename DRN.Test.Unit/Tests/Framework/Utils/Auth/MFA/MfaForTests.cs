using System.Security.Claims;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Framework.Utils.Scope;

namespace DRN.Test.Unit.Tests.Framework.Utils.Auth.MFA;

public class MfaForTests
{
    [Theory]
    [DataInlineUnit]
    public void Exempt_User_Should_Not_Require_Renewal(DrnTestContextUnit context)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "CustomApiKey"));
        var scopedUser = ScopedUser.FromClaimsPrincipal(principal);
        scopedUser.SetExemption("CustomApiKey", principal);
        ScopeContext.InitializeForTest(context, scopedUser: scopedUser);

        scopedUser.Authenticated.Should().BeTrue();
        scopedUser.HasExemptionScheme.Should().BeTrue();
        MfaFor.MfaCompleted.Should().BeFalse();
        MfaFor.MfaRenewalRequired.Should().BeFalse();
    }
}
