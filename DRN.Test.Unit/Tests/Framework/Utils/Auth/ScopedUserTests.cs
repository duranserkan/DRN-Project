using System.Security.Claims;
using DRN.Framework.Utils.Auth;

namespace DRN.Test.Unit.Tests.Framework.Utils.Auth;

public class ScopedUserTests
{
    private const string ClaimType = "permission";
    private const string NumericIssuer = "numeric";

    [Fact]
    public void FromClaimsPrincipal_Should_Treat_Empty_Principal_As_Anonymous()
    {
        var scopedUser = ScopedUser.FromClaimsPrincipal(new ClaimsPrincipal());

        scopedUser.Authenticated.Should().BeFalse();
        scopedUser.PrimaryIdentity.Should().BeNull();
        scopedUser.ClaimsByType.Should().BeEmpty();
    }

    [Fact]
    public void FromClaimsPrincipal_Should_Use_Authenticated_Identities_Only()
    {
        var unauthenticatedIdentity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "untrusted-user")]);
        var authenticatedIdentity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "trusted-user")],
            authenticationType: "Test");
        var principal = new ClaimsPrincipal([unauthenticatedIdentity, authenticatedIdentity]);

        var scopedUser = ScopedUser.FromClaimsPrincipal(principal);

        scopedUser.Authenticated.Should().BeTrue();
        scopedUser.PrimaryIdentity.Should().BeSameAs(authenticatedIdentity);
        scopedUser.Id.Should().Be("trusted-user");
        scopedUser.GetClaimValues(ClaimTypes.NameIdentifier).Should().Equal("trusted-user");
    }

    [Fact]
    public void GetClaimParameter_Should_Parse_Per_Call_And_Requested_Target_Type()
    {
        var scopedUser = CreateScopedUser(new Claim(ClaimType, "41", ClaimValueTypes.Integer, NumericIssuer));
        ParseTrackedClaim.Reset();

        scopedUser.GetClaimParameter<int>(ClaimType, NumericIssuer).Should().Be(41);
        scopedUser.GetClaimParameter<ParseTrackedClaim>(ClaimType, NumericIssuer).Should().Be(new ParseTrackedClaim(41));
        scopedUser.GetClaimParameter<ParseTrackedClaim>(ClaimType, NumericIssuer).Should().Be(new ParseTrackedClaim(41));

        ParseTrackedClaim.ParseCount.Should().Be(2);
    }

    [Theory]
    [DataInlineUnit(null, 41, 41, 0)]
    [DataInlineUnit("invalid", 41, 41, 2)]
    [DataInlineUnit("41", 99, 41, 2)]
    public void GetClaimParameter_Should_Resolve_Typed_Fallback_For_Concrete_And_Default_Interface_Implementations(
        string? claimValue, int fallbackValue, int expectedValue, int expectedParseCount)
    {
        var scopedUser = claimValue == null
            ? CreateScopedUser()
            : CreateScopedUser(new Claim(ClaimType, claimValue, ClaimValueTypes.Integer, NumericIssuer));
        IScopedUser interfaceDefault = new DefaultGetClaimParameterScopedUser(scopedUser);
        var fallback = new ParseTrackedClaim(fallbackValue);
        var expected = new ParseTrackedClaim(expectedValue);
        ParseTrackedClaim.Reset();

        scopedUser.GetClaimParameter(ClaimType, NumericIssuer, fallback).Should().Be(expected);
        interfaceDefault.GetClaimParameter(ClaimType, NumericIssuer, fallback).Should().Be(expected);

        ParseTrackedClaim.ParseCount.Should().Be(expectedParseCount);
    }

    [Fact]
    public void GetClaimParameter_Should_Return_Null_For_Unparsable_Reference_Claim()
    {
        var scopedUser = CreateScopedUser(new Claim(ClaimType, "invalid", ClaimValueTypes.String, NumericIssuer));
        ReferenceParseTrackedClaim.Reset();

        scopedUser.GetClaimParameter<ReferenceParseTrackedClaim>(ClaimType, NumericIssuer).Should().BeNull();
        scopedUser.GetClaimParameter<ReferenceParseTrackedClaim>(ClaimType, NumericIssuer).Should().BeNull();

        ReferenceParseTrackedClaim.ParseCount.Should().Be(2);
    }

    [Fact]
    public void GetClaimParameter_Should_Read_Changed_User()
    {
        var scopedUser = CreateScopedUser(new Claim(ClaimType, "41", ClaimValueTypes.Integer, NumericIssuer));
        scopedUser.GetClaimParameter<int>(ClaimType, NumericIssuer).Should().Be(41);

        scopedUser.SetUser(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimType, "42", ClaimValueTypes.Integer, NumericIssuer)],
            authenticationType: "Test")));

        scopedUser.GetClaimParameter<int>(ClaimType, NumericIssuer).Should().Be(42);
    }

    [Fact]
    public void ScopedUser_Should_Track_Exemption_Scheme_And_Principal()
    {
        var scopedUser = ScopedUser.FromClaimsPrincipal(new ClaimsPrincipal());
        scopedUser.HasExemptionScheme.Should().BeFalse();
        scopedUser.ExemptionScheme.Should().BeNull();
        scopedUser.ExemptionPrincipal.Should().BeNull();

        var certPrincipal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("thumbprint", "abc")], "ClientCert"));
        scopedUser.SetExemption("ClientCert", certPrincipal);

        scopedUser.HasExemptionScheme.Should().BeTrue();
        scopedUser.Exemption.Should().Be(new ExemptionProof("ClientCert", certPrincipal));
        scopedUser.ExemptionScheme.Should().Be("ClientCert");
        scopedUser.ExemptionPrincipal.Should().BeSameAs(certPrincipal);

        var apiKeyPrincipal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("key", "xyz")], "CustomApiKey"));
        scopedUser.SetExemption("CustomApiKey", apiKeyPrincipal);

        scopedUser.HasExemptionScheme.Should().BeTrue();
        scopedUser.Exemption.Should().Be(new ExemptionProof("CustomApiKey", apiKeyPrincipal));
        scopedUser.ExemptionScheme.Should().Be("CustomApiKey");
        scopedUser.ExemptionPrincipal.Should().BeSameAs(apiKeyPrincipal);

        scopedUser.SetExemption("UnauthenticatedScheme", new ClaimsPrincipal());
        scopedUser.HasExemptionScheme.Should().BeFalse();
        scopedUser.Exemption.Should().BeNull();
        scopedUser.ExemptionScheme.Should().BeNull();
        scopedUser.ExemptionPrincipal.Should().BeNull();

        scopedUser.SetExemption(null);
        scopedUser.HasExemptionScheme.Should().BeFalse();
        scopedUser.Exemption.Should().BeNull();
        scopedUser.ExemptionScheme.Should().BeNull();
        scopedUser.ExemptionPrincipal.Should().BeNull();
    }

    private static ScopedUser CreateScopedUser(params Claim[] claims) =>
        ScopedUser.FromClaimsPrincipal(new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test")));

    private readonly record struct ParseTrackedClaim(int Value) : IParsable<ParseTrackedClaim>
    {
        private static int _parseCount;
        internal static int ParseCount => Volatile.Read(ref _parseCount);
        internal static void Reset() => Interlocked.Exchange(ref _parseCount, 0);

        public static ParseTrackedClaim Parse(string value, IFormatProvider? provider) =>
            new(int.Parse(value, provider));

        public static bool TryParse(string? value, IFormatProvider? provider, out ParseTrackedClaim result)
        {
            Interlocked.Increment(ref _parseCount);
            if (int.TryParse(value, provider, out var parsedValue))
            {
                result = new ParseTrackedClaim(parsedValue);
                return true;
            }

            result = default;
            return false;
        }
    }

    private sealed class ReferenceParseTrackedClaim : IParsable<ReferenceParseTrackedClaim>
    {
        private static int _parseCount;
        internal static int ParseCount => Volatile.Read(ref _parseCount);
        internal static void Reset() => Interlocked.Exchange(ref _parseCount, 0);

        public static ReferenceParseTrackedClaim Parse(string value, IFormatProvider? provider) =>
            throw new FormatException($"'{value}' is not a valid {nameof(ReferenceParseTrackedClaim)}.");

        public static bool TryParse(string? value, IFormatProvider? provider, out ReferenceParseTrackedClaim result)
        {
            Interlocked.Increment(ref _parseCount);
            result = null!;
            return false;
        }
    }

    private sealed class DefaultGetClaimParameterScopedUser(IScopedUser user) : IScopedUser
    {
        public ClaimsPrincipal? Principal => user.Principal;
        public ClaimsIdentity? PrimaryIdentity => user.PrimaryIdentity;
        public bool Authenticated => user.Authenticated;
        public string? Id => user.Id;
        public ClaimGroup? IdClaim => user.IdClaim;
        public string? Name => user.Name;
        public ClaimGroup? NameClaim => user.NameClaim;
        public string? Email => user.Email;
        public ClaimGroup? EmailClaim => user.EmailClaim;
        public string? Amr => user.Amr;
        public ClaimGroup? AmrClaim => user.AmrClaim;
        public string? AuthenticationMethod => user.AuthenticationMethod;
        public ClaimGroup? AuthenticationMethodClaim => user.AuthenticationMethodClaim;
        public ClaimGroup? RoleClaim => user.RoleClaim;
        public ExemptionProof? Exemption => user.Exemption;
        public string? ExemptionScheme => user.ExemptionScheme;
        public ClaimsPrincipal? ExemptionPrincipal => user.ExemptionPrincipal;
        public bool HasExemptionScheme => user.HasExemptionScheme;
        public IReadOnlyDictionary<string, ClaimGroup> ClaimsByType => user.ClaimsByType;

        public bool IsInRole(string role) => user.IsInRole(role);
        public ClaimGroup? FindClaimGroup(string type) => user.FindClaimGroup(type);
        public Claim? FindClaim(string type, string value, string? issuer = null) => user.FindClaim(type, value, issuer);
        public IReadOnlyList<Claim> FindClaims(string type, string? issuer = null) => user.FindClaims(type, issuer);
        public bool ClaimExists(string type, string? issuer = null) => user.ClaimExists(type, issuer);
        public bool ValueExists(string type, string value, string? issuer = null) => user.ValueExists(type, value, issuer);
        public string GetClaimValue(string claim, string? issuer = null, string defaultValue = "") =>
            user.GetClaimValue(claim, issuer, defaultValue);
        public IReadOnlyList<string> GetClaimValues(string claim, string? issuer = null) => user.GetClaimValues(claim, issuer);
    }
}
