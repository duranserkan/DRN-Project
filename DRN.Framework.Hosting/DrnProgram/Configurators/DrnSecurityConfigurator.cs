using System.Security.Claims;
using DRN.Framework.Hosting.Auth;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Hosting.Consent;
using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.Hosting.Middlewares;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Data.Encodings;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NetEscapades.AspNetCore.SecurityHeaders.Infrastructure;

namespace DRN.Framework.Hosting.DrnProgram.Configurators;

internal static class DrnSecurityConfigurator
{
    internal static void ConfigureIdentityClaims(IdentityOptions options, AuthenticationClaimConfig claims)
    {
        options.ClaimsIdentity.UserIdClaimType = claims.Subject.Type;
        options.ClaimsIdentity.UserNameClaimType = claims.Name.Type;
        options.ClaimsIdentity.EmailClaimType = claims.Email.Type;
        options.ClaimsIdentity.RoleClaimType = claims.Roles.Type;
    }

    internal static void AddAntiforgery(IServiceCollection services, IAppSettings appSettings)
    {
        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = appSettings.GetAppSpecificName("Antiforgery");
            options.Cookie.IsEssential = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.HttpOnly = true;
        });
    }

    internal static void ConfigureDefaultSecurityHeaders(HeaderPolicyCollection policies, IAppSettings appSettings,
        Action<CspBuilder> configureDefaultCsp)
    {
        var policyCollection = policies.RemoveServerHeader()
            .AddFrameOptionsDeny()
            .AddContentTypeOptionsNoSniff()
            .AddReferrerPolicyStrictOriginWhenCrossOrigin()
            .AddContentSecurityPolicy(configureDefaultCsp)
            .AddCrossOriginOpenerPolicy(x => x.SameOrigin())
            .AddCrossOriginEmbedderPolicy(builder => builder.Credentialless())
            .AddCrossOriginResourcePolicy(builder => builder.SameSite())
            .AddPermissionsPolicy(builder =>
            {
                builder.AddDefaultSecureDirectives();
                builder.AddFullscreen().Self();
            });

        if (!appSettings.IsDevelopmentEnvironment)
        {
            // Keep preload disabled: certificate renewal/recovery must remain under the application's control.
            // https://hstspreload.org/ preload can be risky.
            // Monitor proactively, automate renewal, and test staging first.
            // Keep an emergency response plan to deploy a certificate fix in < 5 min (e.g., via CI/CD or infra-as-code).
            policyCollection.AddStrictTransportSecurity(63072000, true, false);
        }
    }

    internal static void ConfigureDefaultCsp(CspBuilder builder, Action<CspBuilder> configureDefaultCspBase)
    {
        configureDefaultCspBase(builder);
        builder.AddScriptSrc().WithNonce();
    }

    internal static void ConfigureDefaultCspBase(CspBuilder builder)
    {
        builder.AddDefaultSrc().None();
        builder.AddBaseUri().Self();
        builder.AddFormAction().Self();

        builder.AddObjectSrc().None();
        builder.AddFrameAncestors().None();
        builder.AddScriptSrcAttr().None();
        builder.AddStyleSrc().Self().WithNonce();
        builder.AddStyleSrcAttr().UnsafeInline();
        builder.AddImgSrc().Self().Data();
        builder.AddConnectSrc().Self();
        builder.AddFontSrc().Self().Data();
        builder.AddMediaSrc().Self();
        builder.AddManifestSrc().Self();
        builder.AddWorkerSrc().Self().Blob();
    }

    internal static void ConfigureSecurityHeaderPolicyBuilder(SecurityHeaderPolicyBuilder builder,
        IServiceProvider serviceProvider, IAppSettings appSettings, DrnProgramSwaggerOptions swaggerOptions,
        Action<HeaderPolicyCollection, IServiceProvider, IAppSettings> configureDefaultSecurityHeaders,
        Action<CspBuilder> configureDefaultCspBase)
    {
        //todo: csp policy dictionary
        var selfCsp = new HeaderPolicyCollection();
        configureDefaultSecurityHeaders(selfCsp, serviceProvider, appSettings);
        selfCsp.Remove("Content-Security-Policy");
        selfCsp.AddContentSecurityPolicy(x =>
        {
            configureDefaultCspBase(x);
            x.AddScriptSrc().Self();
        });
        builder.AddPolicy(CspFor.CspPolicySelf, selfCsp);

        var swaggerCsp = new HeaderPolicyCollection();
        configureDefaultSecurityHeaders(swaggerCsp, serviceProvider, appSettings);
        swaggerCsp.Remove("Content-Security-Policy");
        swaggerCsp.AddContentSecurityPolicy(x =>
        {
            configureDefaultCspBase(x);
            x.AddScriptSrc().Self();
            // Swagger renders inline SVG styles. Replace the nonce directive so unsafe-inline is effective.
            x.AddStyleSrc().Self().UnsafeInline();
        });
        builder.AddPolicy(CspFor.CspPolicySwagger, swaggerCsp);

        var inlineCspPolicy = new HeaderPolicyCollection();
        configureDefaultSecurityHeaders(inlineCspPolicy, serviceProvider, appSettings);
        inlineCspPolicy.Remove("Content-Security-Policy");
        inlineCspPolicy.AddContentSecurityPolicy(x =>
        {
            configureDefaultCspBase(x);
            x.AddScriptSrc().Self().UnsafeInline();
        });
        builder.AddPolicy(CspFor.CspPolicyInline, inlineCspPolicy);

        builder.SetPolicySelector(x =>
        {
            var context = x.HttpContext;
            if (swaggerOptions.SwaggerUIPathPrefix is { } prefix &&
                context.Request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
                return x.ConfiguredPolicies[CspFor.CspPolicySwagger];

            var policyApplied = context.Items.TryGetValue(CspFor.CspPolicyName, out var policy);
            if (!policyApplied)
                return x.DefaultPolicy;

            return (policy as string) switch
            {
                CspFor.CspPolicySelf => x.ConfiguredPolicies[CspFor.CspPolicySelf],
                CspFor.CspPolicyInline => x.ConfiguredPolicies[CspFor.CspPolicyInline],
                _ => x.DefaultPolicy
            };
        });
    }

    internal static void ConfigureCookiePolicy(CookiePolicyOptions options, IAppSettings appSettings)
    {
        // https://learn.microsoft.com/en-us/aspnet/core/security/gdpr
        options.HttpOnly = HttpOnlyPolicy.None; // Consent cookies are script-readable under strict CSP.
        options.MinimumSameSitePolicy = SameSiteMode.Strict;
        options.Secure = appSettings.IsDevelopmentEnvironment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;

        options.ConsentCookieValue = ConsentCookie.DefaultValue.Encode();
        // The default cookie name (.AspNet.Consent) exposes the server.
        options.ConsentCookie.Name = appSettings.GetAppSpecificName("CookieConsent");
        options.CheckConsentNeeded = _ => true; // User consent is required for non-essential cookies.
    }

    internal static void ConfigureCookieTempDataProvider(CookieTempDataProviderOptions options)
    {
        // https://learn.microsoft.com/en-us/aspnet/core/fundamentals/app-state
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
    }

    internal static void ConfigureSecurityStampValidatorOptions(SecurityStampValidatorOptions options, AuthenticationClaimConfig claims)
    {
        var previous = options.OnRefreshingPrincipal;
        options.OnRefreshingPrincipal = async context =>
        {
            var current = context.CurrentPrincipal
                          ?? throw new System.Security.SecurityException("Cannot renew without an original principal.");
            // ClaimsPrincipal.Clone shares identities; snapshot their claims before consumer callbacks can mutate them.
            var original = new ClaimsPrincipal(current.Identities.Select(identity => identity.Clone()));
            if (previous != null)
                await previous(context);
            if (context.NewPrincipal is not { } renewed || !MfaClaimPreservation.Preserve(original, renewed, claims))
                throw new System.Security.SecurityException("Cannot renew a missing principal or one with conflicting account evidence.");
        };
    }

    internal static void ConfigureMfa(WebApplication application,
        Func<MfaExemptionConfig?> configureMfaExemption, Func<MfaRedirectionConfig?> configureMfaRedirection)
    {
        // Resolve each options instance before invoking its hook, preserving startup customization order.
        var exemptionOptions = application.Services.GetRequiredService<MfaExemptionOptions>();
        var exemptionConfig = configureMfaExemption();
        if (exemptionConfig != null)
        {
            exemptionOptions.MapFromConfig(exemptionConfig);
            application.UseMiddleware<MfaExemptionMiddleware>();
        }

        var redirectionOptions = application.Services.GetRequiredService<MfaRedirectionOptions>();
        var redirectionConfig = configureMfaRedirection();
        if (redirectionConfig != null)
        {
            redirectionOptions.MapFromConfig(redirectionConfig);
            application.UseMiddleware<MfaRedirectionMiddleware>();
        }
    }

    internal static void ConfigureAuthorizationOptions(AuthorizationOptions options)
    {
        options.AddPolicy(AuthPolicy.Mfa, policy => policy.AddRequirements(new MfaRequirement()));
        options.AddPolicy(AuthPolicy.MfaExempt, policy => policy.AddRequirements(new MfaExemptRequirement()));

        options.DefaultPolicy = options.GetPolicy(AuthPolicy.Mfa)!;
        options.FallbackPolicy = options.GetPolicy(AuthPolicy.Mfa)!;
    }
}
