using System.Security.Claims;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;

namespace DRN.Test.Unit.Tests.Framework.Utils.Auth.MFA;

public class MfaPrincipalTests
{
    [Fact]
    public void SingleAccount_Should_Preserve_Anonymous_Single_And_Multiple_Identity_Boundaries()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        MfaPrincipal.HasSingleAccount(principal).Should().BeFalse();

        var first = new ClaimsIdentity(authenticationType: "First");
        principal.AddIdentity(first);
        MfaPrincipal.HasSingleAccount(principal).Should().BeTrue();

        var second = new ClaimsIdentity(authenticationType: "Second");
        principal.AddIdentity(second);
        MfaPrincipal.HasSingleAccount(principal).Should().BeFalse();

        first.AddClaim(new Claim("account", "same", ClaimValueTypes.String, "issuer"));
        second.AddClaim(new Claim("account", "same", ClaimValueTypes.String, "issuer"));
        var mapping = new AuthenticationClaimConfig { Subject = new("account") };
        MfaPrincipal.HasSingleAccount(principal, mapping).Should().BeTrue();
        MfaPrincipal.HasSingleAccount(principal).Should().BeFalse();

        second.RemoveClaim(second.FindFirst("account")!);
        second.AddClaim(new Claim("account", "same", ClaimValueTypes.String, "other-issuer"));
        MfaPrincipal.HasSingleAccount(principal, mapping).Should().BeFalse();

        second.RemoveClaim(second.FindFirst("account")!);
        second.AddClaim(new Claim("account", "different", ClaimValueTypes.String, "issuer"));
        MfaPrincipal.HasSingleAccount(principal, mapping).Should().BeFalse();
    }

    [Theory]
    [DataMemberUnit(nameof(CompletionCases))]
    public void Completion_Should_Use_Authenticated_Unrestricted_Evidence(
        string scenario, ClaimsPrincipal principal, AuthenticationClaimConfig config, bool expected)
    {
        MfaPrincipal.IsCompleted(principal, config).Should().Be(expected, scenario);
    }

    public static IEnumerable<object[]> CompletionCases()
    {
        var config = AuthenticationClaimConfig.Default;
        yield return ["anonymous", new ClaimsPrincipal(new ClaimsIdentity()), config, false];
        yield return ["password only", new ClaimsPrincipal(Identity("Password", "pwd")), config, false];
        yield return ["completed", new ClaimsPrincipal(Identity("Password", "pwd", MfaClaimValues.Amr)), config, true];
        yield return ["unauthenticated MFA claim", new ClaimsPrincipal([
            Identity("Password", "pwd"), Identity(null, MfaClaimValues.Amr)]), config, false];
        yield return ["authenticated secondary identity", new ClaimsPrincipal([
            Identity("Password", "pwd"), Identity("Federated", MfaClaimValues.Amr)]), config, true];

        var setup = new ClaimsIdentity([new Claim(ClaimConventions.AuthenticationMethod, MfaClaimValues.MfaSetupRequired)]);
        yield return ["unauthenticated setup is ignored", new ClaimsPrincipal([
            Identity("Password", MfaClaimValues.Amr), setup]), config, true];

        foreach (var state in new[] { MfaClaimValues.MfaSetupRequired, MfaClaimValues.MfaInProgress })
        {
            var restricted = Identity("Password", MfaClaimValues.Amr);
            restricted.AddClaim(new Claim(ClaimConventions.AuthenticationMethod, state));
            yield return [state, new ClaimsPrincipal(restricted), config, false];
        }

        var customConfig = new AuthenticationClaimConfig { Mfa = new("acr", "urn:drn:test:mfa") };
        var customIdentity = new ClaimsIdentity([new Claim(customConfig.Mfa.ClaimType, customConfig.Mfa.ClaimValue)], "Federated");
        yield return ["configured MFA claim", new ClaimsPrincipal(customIdentity), customConfig, true];
    }

    private static ClaimsIdentity Identity(string? authenticationType, params string[] methods) =>
        new(methods.Select(method => new Claim(ClaimConventions.AuthenticationMethodReference, method))
            .Append(new Claim(ClaimTypes.NameIdentifier, "same-user")), authenticationType);
}
