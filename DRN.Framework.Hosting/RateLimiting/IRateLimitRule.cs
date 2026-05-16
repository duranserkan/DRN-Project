using System.Threading.RateLimiting;
using DRN.Framework.Hosting.Middlewares;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.RateLimiting;

/// <summary>
/// Defines a pluggable rate limiting rule evaluated by the dual-layer rate limiting middleware.
/// <para>
/// <b>Pre-auth layer</b>: <see cref="PreAuthRateLimitingMiddleware"/> calls <see cref="EvaluatePreAuth"/>.
/// <b>Post-auth layer</b>: Standard .NET <c>UseRateLimiter()</c> calls <see cref="EvaluatePostAuth"/>.
/// </para>
/// <para>
/// Rules are resolved by lifetime-specific interfaces and evaluated in ascending
/// <see cref="Order"/>. Return <c>null</c> from evaluate methods to skip the rule. Return a
/// <see cref="RateLimitRuleResult"/> to select a limiter partition.
/// </para>
/// </summary>
/// <remarks>
/// Follows the <c>IDrnExceptionFilter</c> precedent with intentional DIM divergence:
/// <c>IDrnExceptionFilter</c> has 2 required methods. <c>IRateLimitRule</c> uses DIMs because
/// it has optional phase-specific members — requiring all would be high-friction for rules targeting only one phase.
/// </remarks>
public interface IRateLimitRule
{
    /// <summary>
    /// Execution order. Lower values execute first. Framework defaults use <see cref="int.MaxValue"/>
    /// so application-specific rules naturally run before the fallback defaults.
    /// </summary>
    int Order => 0;

    /// <summary>
    /// When true, this rule is evaluated before other rules with the same <see cref="Order"/>,
    /// and a non-null match stops remaining rules for the current phase. Use for allow/deny rules that must short-circuit
    /// normal quota composition.
    /// </summary>
    bool ShortCircuitOnMatch => false;

    /// <summary>
    /// Optional ASP.NET Core rate limiting policy name. <c>null</c> means the rule is global.
    /// Non-null values must be non-empty and apply the rule only to endpoints marked with
    /// <c>[EnableRateLimiting("policy-name")]</c>. Native policies configured through
    /// <c>AddRateLimiter(options =&gt; ...)</c> still run normally.
    /// </summary>
    string? PolicyName => null;

    /// <summary>
    /// Called by <see cref="PreAuthRateLimitingMiddleware"/> before authentication.
    /// Return <c>null</c> to skip this rule for this phase.
    /// </summary>
    /// <remarks>
    /// <b>Scoped-capture warning</b>: the <see cref="RateLimitRuleResult.Partition"/> options factory
    /// is cached per partition key by the .NET rate limiter. Do not capture <c>HttpContext</c> or
    /// scoped services inside the factory lambda — use only value-based parameters.
    /// </remarks>
    RateLimitRuleResult? EvaluatePreAuth(HttpContext context) => null;

    /// <summary>
    /// Called by the post-auth rate limiter after authentication and <c>ScopedUserMiddleware</c>.
    /// Return <c>null</c> to skip this rule for this phase.
    /// </summary>
    /// <remarks>
    /// <b>Scoped-capture warning</b>: the <see cref="RateLimitRuleResult.Partition"/> options factory
    /// is cached per partition key by the .NET rate limiter. Do not capture <c>HttpContext</c> or
    /// scoped services inside the factory lambda — use only value-based parameters.
    /// </remarks>
    RateLimitRuleResult? EvaluatePostAuth(HttpContext context) => null;

    /// <summary>
    /// Called when a request is rejected by this rule's limiter. Override for custom rejection behavior.
    /// The framework handles 429 status code and Retry-After header automatically.
    /// </summary>
    Task OnRejectedAsync(HttpContext context, RateLimitLease lease, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>
/// Marker contract for singleton rate limit rules. Use for stateless rules and settings-backed policies.
/// </summary>
public interface ISingletonRateLimitRule : IRateLimitRule;

/// <summary>
/// Marker contract for scoped rate limit rules. Use only when post-auth evaluation needs scoped collaborators.
/// Scoped rules are not evaluated by the pre-auth limiter.
/// </summary>
public interface IScopedRateLimitRule : IRateLimitRule;

/// <summary>
/// Convenience base class for rate limit rules. Derive from <see cref="SingletonRateLimitRule"/>
/// or <see cref="ScopedRateLimitRule"/> for automatic attribute-based DI registration.
/// <para>
/// Provides <c>virtual</c> members for IDE discoverability. Override only the methods relevant
/// to the phase your rule targets (<see cref="EvaluatePreAuth"/> or <see cref="EvaluatePostAuth"/>).
/// </para>
/// </summary>
public abstract class RateLimitRule : IRateLimitRule
{
    /// <inheritdoc />
    public virtual int Order => 0;

    /// <inheritdoc />
    public virtual bool ShortCircuitOnMatch => false;

    /// <inheritdoc />
    public virtual string? PolicyName => null;

    /// <inheritdoc />
    public virtual RateLimitRuleResult? EvaluatePreAuth(HttpContext context) => null;

    /// <inheritdoc />
    public virtual RateLimitRuleResult? EvaluatePostAuth(HttpContext context) => null;

    /// <inheritdoc />
    public virtual Task OnRejectedAsync(HttpContext context, RateLimitLease lease, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>
/// Convenience base class for singleton rate limit rules.
/// </summary>
[Singleton<ISingletonRateLimitRule>(tryAdd: false)]
public abstract class SingletonRateLimitRule : RateLimitRule, ISingletonRateLimitRule;

/// <summary>
/// Convenience base class for scoped rate limit rules.
/// </summary>
[Scoped<IScopedRateLimitRule>(tryAdd: false)]
public abstract class ScopedRateLimitRule : RateLimitRule, IScopedRateLimitRule;
