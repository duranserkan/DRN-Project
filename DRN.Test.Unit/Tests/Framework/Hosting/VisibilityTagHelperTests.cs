using System.Security.Claims;
using DRN.Framework.Hosting.Auth;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Hosting.TagHelpers;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace DRN.Test.Unit.Tests.Framework.Hosting;

public class VisibilityTagHelperTests
{
    [Theory]
    [DataInlineUnit(true, true, true, true)]
    [DataInlineUnit(false, true, true, false)]
    [DataInlineUnit(true, false, true, false)]
    [DataInlineUnit(true, true, false, false)]
    public async Task PolicyOnly_Should_Evaluate_Authentication_Roles_And_Claims(
        bool authenticated, bool hasRole, bool hasClaim, bool visible)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore(options => options.AddPolicy("Manage", policy => policy
            .RequireAuthenticatedUser().RequireRole("Admin").RequireClaim("permission", "manage")));
        using var provider = services.BuildServiceProvider();
        var claims = new List<Claim>();
        if (hasRole) claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        if (hasClaim) claims.Add(new Claim("permission", "manage"));
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "Test" : null));
        var helper = CreatePolicyHelper(provider, user, "Manage");
        var output = CreateOutput();

        await helper.ProcessAsync(CreateContext(), output);

        output.TagName.Should().Be(visible ? "div" : null);
        output.Content.GetContent().Should().Be(visible ? "Sensitive content" : string.Empty);
    }

    [Fact]
    public async Task PolicyOnly_Should_Use_Current_Http_User_And_Explicit_Resource()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "Current"));
        var resource = new object();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore(options => options.AddPolicy("Resource", policy => policy
            .RequireAssertion(context => Task.FromResult(
                ReferenceEquals(context.User, user) && ReferenceEquals(context.Resource, resource)))));
        using var provider = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext();
        var helper = new PolicyOnlyTagHelper(provider.GetRequiredService<IAuthorizationService>())
        {
            PolicyOnly = "Resource",
            PolicyResource = resource,
            ViewContext = new ViewContext { HttpContext = httpContext }
        };
        // Resolve the final request principal at processing time, not construction time.
        httpContext.User = user;
        var allowed = CreateOutput();
        await helper.ProcessAsync(CreateContext(), allowed);
        allowed.TagName.Should().Be("div");

        helper.PolicyResource = null;
        var denied = CreateOutput();
        await helper.ProcessAsync(CreateContext(), denied);
        denied.TagName.Should().BeNull();
    }

    [Theory]
    [DataInlineUnit("")]
    [DataInlineUnit(" ")]
    public async Task PolicyOnly_Should_Reject_Blank_Policy_Names(string policy)
    {
        var service = Substitute.For<IAuthorizationService>();
        var helper = new PolicyOnlyTagHelper(service) { PolicyOnly = policy };

        var act = () => helper.ProcessAsync(CreateContext(), CreateOutput());

        await act.Should().ThrowAsync<ArgumentException>();
        service.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task PolicyOnly_Should_Propagate_Missing_Policy_Error()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore();
        using var provider = services.BuildServiceProvider();
        var helper = CreatePolicyHelper(provider, new ClaimsPrincipal(), "Missing");

        var act = () => helper.ProcessAsync(CreateContext(), CreateOutput());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [DataInlineUnit(false, false)]
    [DataInlineUnit(true, true)]
    public async Task PolicyOnly_Should_Preserve_Drn_Default_Mfa_Requirement(bool completed, bool visible)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder().AddRequirements(new MfaRequirement()).Build();
            options.AddPolicy("Manage", policy => policy.RequireClaim("permission", "manage"));
        });
        services.AddSingleton<IAuthorizationPolicyProvider, MfaEnforcingAuthorizationPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, RequireMfaHandler>();
        using var provider = services.BuildServiceProvider();
        var claims = new List<Claim> { new("permission", "manage") };
        if (completed) claims.Add(new Claim("amr", "mfa"));
        var helper = CreatePolicyHelper(provider, new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")), "Manage");
        var output = CreateOutput();

        await helper.ProcessAsync(CreateContext(), output);

        output.TagName.Should().Be(visible ? "div" : null);
    }

    [Fact]
    public async Task PolicyOnly_Should_Default_To_Null_Resource_And_Preserve_Previous_Suppression()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore(options => options.AddPolicy("NoResource", policy => policy
            .RequireAssertion(context => context.Resource == null)));
        using var provider = services.BuildServiceProvider();
        var helper = CreatePolicyHelper(provider, new ClaimsPrincipal(), "NoResource");
        var output = CreateOutput();

        await helper.ProcessAsync(CreateContext(), output);
        output.TagName.Should().Be("div");
        output.SuppressOutput();
        await helper.ProcessAsync(CreateContext(), output);
        output.TagName.Should().BeNull();
        output.Content.GetContent().Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit(true, true, "")]
    [DataInlineUnit(true, false, "")]
    [DataInlineUnit(false, true, "")]
    [DataInlineUnit(false, false, "")]
    [DataInlineUnit(true, true, MfaClaimValues.MfaSetupRequired)]
    [DataInlineUnit(true, true, MfaClaimValues.MfaInProgress)]
    public void AuthorizedOnly_Should_Require_Only_Authentication(DrnTestContextUnit context, bool minimized, bool authenticated, string mfaState)
    {
        Claim[] claims = mfaState.Length > 0 ? [new Claim(ClaimConventions.AuthenticationMethod, mfaState)] : [];
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "Test" : null));
        ScopeContext.InitializeForTest(context, scopedUser: ScopedUser.FromClaimsPrincipal(principal));
        var output = CreateOutput();
        var attribute = minimized ? new TagHelperAttribute("authorized-only") : new TagHelperAttribute("authorized-only", "false");
        output.Attributes.Add(attribute);
        var tagContext = new TagHelperContext(new TagHelperAttributeList { attribute }, new Dictionary<object, object>(), "visibility");

        new AuthorizedOnlyTagHelper().Process(tagContext, output);

        output.TagName.Should().Be(authenticated ? "div" : null);
        output.Attributes.ContainsName("authorized-only").Should().BeFalse();
    }

    [Theory]
    [DataInlineUnit(true, true)]
    [DataInlineUnit(true, false)]
    [DataInlineUnit(false, true)]
    [DataInlineUnit(false, false)]
    public void AnonymousOnly_Should_Filter_Regardless_Of_Attribute_Value(DrnTestContextUnit context, bool minimized, bool authenticated)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: authenticated ? "Test" : null));
        ScopeContext.InitializeForTest(context, scopedUser: ScopedUser.FromClaimsPrincipal(principal));
        var output = CreateOutput();
        var attribute = minimized ? new TagHelperAttribute("anonymous-only") : new TagHelperAttribute("anonymous-only", "false");
        output.Attributes.Add(attribute);
        var tagContext = new TagHelperContext(new TagHelperAttributeList { attribute }, new Dictionary<object, object>(), "visibility");

        new AnonymousOnlyTagHelper().Process(tagContext, output);

        output.TagName.Should().Be(authenticated ? null : "div");
        output.Attributes.ContainsName("anonymous-only").Should().BeFalse();
    }

    private static PolicyOnlyTagHelper CreatePolicyHelper(IServiceProvider provider, ClaimsPrincipal user, string policy) =>
        new(provider.GetRequiredService<IAuthorizationService>())
        {
            PolicyOnly = policy,
            ViewContext = new ViewContext { HttpContext = new DefaultHttpContext { User = user } }
        };

    private static TagHelperContext CreateContext() => new(new TagHelperAttributeList(), new Dictionary<object, object>(), "visibility");

    private static TagHelperOutput CreateOutput()
    {
        var output = new TagHelperOutput("div", new TagHelperAttributeList(),
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        output.Content.SetContent("Sensitive content");
        return output;
    }
}
