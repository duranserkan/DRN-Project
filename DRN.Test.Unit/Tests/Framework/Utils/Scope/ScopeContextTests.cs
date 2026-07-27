using System.Security.Claims;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Scope;

namespace DRN.Test.Unit.Tests.Framework.Utils.Scope;

public class ScopeContextTests
{
    private const string ClaimType = "permission";
    private const string TrustedIssuer = "trusted";
    private const string NumericIssuer = "numeric";
    private const string MissingIssuer = "missing";

    [Theory]
    [DataInlineUnit]
    public void IsClaimFlagEnabled_Should_Resolve_Issuer_And_Default_Per_Call(DrnTestContextUnit context)
    {
        InitializeScope(context,
            new Claim(ClaimType, bool.FalseString, ClaimValueTypes.Boolean, TrustedIssuer));

        ScopeContext.IsClaimFlagEnabled(ClaimType, MissingIssuer, defaultValue: true).Should().BeTrue();
        ScopeContext.IsClaimFlagEnabled(ClaimType, TrustedIssuer, defaultValue: true).Should().BeFalse();
        ScopeContext.Data.Flags.Should().NotContainKey(ClaimType);
    }

    [Theory]
    [DataInlineUnit]
    public void GetClaimParameter_Should_Resolve_Issuer_Type_And_Default_Per_Call(DrnTestContextUnit context)
    {
        InitializeScope(context,
            new Claim(ClaimType, bool.FalseString, ClaimValueTypes.Boolean, TrustedIssuer),
            new Claim(ClaimType, "41", ClaimValueTypes.Integer, NumericIssuer));

        ScopeContext.GetClaimParameter<bool>(ClaimType, MissingIssuer, defaultValue: true).Should().BeTrue();
        ScopeContext.GetClaimParameter<int>(ClaimType, MissingIssuer, defaultValue: 42).Should().Be(42);
        ScopeContext.GetClaimParameter<int>(ClaimType, MissingIssuer, defaultValue: 43).Should().Be(43);
        ScopeContext.GetClaimParameter<int>(ClaimType, NumericIssuer).Should().Be(41);
        ScopeContext.HasClaimValue(ClaimType, expectedValue: false, issuer: TrustedIssuer).Should().BeTrue();
        ScopeContext.Data.Parameters.Should().NotContainKey(ClaimType);
    }

    [Theory]
    [DataInlineUnit]
    public void IsUserInRole_Should_Delegate_To_ScopedUser(DrnTestContextUnit context)
    {
        InitializeScope(context,
            new Claim(ClaimTypes.Role, "admin", ClaimValueTypes.String, TrustedIssuer));

        ScopeContext.IsUserInRole("admin").Should().BeTrue();
        ScopeContext.IsUserInRole("operator").Should().BeFalse();
    }

    private static void InitializeScope(DrnTestContextUnit context, params Claim[] claims)
    {
        ScopeContext.InitializeForTest(context, scopedUser: CreateScopedUser(claims));
    }

    private static ScopedUser CreateScopedUser(params Claim[] claims) =>
        ScopedUser.FromClaimsPrincipal(new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test")));
}
