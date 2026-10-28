using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using DRN.Framework.SharedKernel;
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Hosting.Middlewares.ApiErrors;
using DRN.Framework.Hosting.RateLimiting;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Scope;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DRN.Test.Utils.Hosting.ApiErrors;

public sealed class ApiErrorTestProgram : DrnProgramBase<ApiErrorTestProgram>, IDrnProgram
{
    public const string Root = "/api-error-probe";
    public const string Secret = "private-exception-password";
    public static Task Main(string[] args) => RunAsync(args);

    protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog)
    {
        builder.Services.AddAuthentication(IdentityConstants.BearerScheme)
            .AddBearerToken(IdentityConstants.BearerScheme)
            .AddCookie("Browser");
        builder.Services.Configure<DrnApiErrorOptions>(options => options.PathPrefixes = [Root]);
        builder.Services.Configure<JsonOptions>(options =>
        {
            options.JsonSerializerOptions.Converters.Clear();
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        });
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Clear();
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower;
        });
        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            var extensions = context.ProblemDetails.Extensions;
            extensions["customizationCount"] = extensions.TryGetValue("customizationCount", out var count) ? (int)count! + 1 : 1;
            extensions["requestPath"] = context.HttpContext.Request.Path.Value;
            extensions["serializationProbe"] = new { DisplayName = "public", LargeId = long.MaxValue, Kind = DayOfWeek.Monday };
        });
        builder.Services.AddSingleton<ISingletonRateLimitRule, ProbeRejectionRule>();
        return Task.CompletedTask;
    }

    protected override void ConfigureSwaggerOptions(DrnProgramSwaggerOptions options, IAppSettings appSettings) => options.AddSwagger = true;

    protected override void ConfigurePostAuthRateLimiterOptions(RateLimiterOptions options, IServiceProvider services, IAppSettings settings)
    {
        base.ConfigurePostAuthRateLimiterOptions(options, services, settings);
        if (settings.Configuration["ApiErrorProbe:Reject503"] == "true")
            options.RejectionStatusCode = 503;
    }

    protected override void ConfigureApplicationPipelineStart(WebApplication application, IAppSettings appSettings)
    {
        base.ConfigureApplicationPipelineStart(application, appSettings);
        application.UsePathBase("/gateway");
    }

    protected override void MapApplicationEndpoints(WebApplication application, IAppSettings appSettings)
    {
        base.MapApplicationEndpoints(application, appSettings);
        application.MapGet(Root + "/minimal", () => TypedResults.NotFound()).DisableCookieRedirect().AllowAnonymous();
        application.MapGet(Root + "/protected", () => "protected")
            .DisableCookieRedirect().RequireAuthorization();
        application.MapGet(Root + "/browser", () => Results.Challenge(authenticationSchemes: ["Browser"]))
            .AllowAnonymous();
        application.MapGet(Root + "/token", () => Results.SignIn(
                new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, "probe-user"), new Claim("amr", "mfa")
                ], IdentityConstants.BearerScheme)), authenticationScheme: IdentityConstants.BearerScheme))
            .AllowAnonymous();
    }

    private sealed class ProbeRejectionRule : ISingletonRateLimitRule
    {
        public int Order => int.MinValue;
        public RateLimitRuleResult? EvaluatePreAuth(HttpContext context) =>
            context.Request.Path == Root + "/pre-limit" ? RateLimitRuleResult.DenyRequest("probe-pre", TimeSpan.FromSeconds(7)) : null;
        public RateLimitRuleResult? EvaluatePostAuth(HttpContext context) =>
            context.Request.Path == Root + "/post-limit" ? RateLimitRuleResult.DenyRequest("probe-post", TimeSpan.FromSeconds(11)) : null;
        public Task OnRejectedAsync(HttpContext context, RateLimitLease lease, CancellationToken cancellationToken = default)
        {
            context.Response.Headers["X-Rejection-Callback"] = "preserved";
            return Task.CompletedTask;
        }
    }
}

[ApiController]
[AllowAnonymous]
[Route(ApiErrorTestProgram.Root)]
public sealed class ApiErrorProbeController : ControllerBase
{
    [HttpGet("status/{status:int}")]
    [HttpHead("status/{status:int}")]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult Status(int status) => StatusCode(status);

    [HttpGet("typed/{status:int}")]
    public IResult Typed(int status) => TypedResults.StatusCode(status);

    [HttpGet("throw/{status:int}")]
    public IActionResult Throw(int status) => throw new BadHttpRequestException(ApiErrorTestProgram.Secret, status);

    [HttpGet("unhandled")]
    public IActionResult Unhandled() => throw new InvalidOperationException(ApiErrorTestProgram.Secret);

    [HttpGet("scoped/{producer}")]
    public IResult Scoped(string producer)
    {
        ScopeContext.Problem.Title = "Public title";
        ScopeContext.Problem.Detail = "Public detail";
        ScopeContext.Problem.Extensions["code"] = "public_code";
        if (producer == "scoped-conflict") ScopeContext.Problem.Conflict(ApiErrorTestProgram.Secret);
        return producer switch
        {
            "status" => TypedResults.StatusCode(409),
            "typed" => TypedResults.Problem("Explicit detail", statusCode: 409),
            "exception-for" => throw ExceptionFor.Conflict(ApiErrorTestProgram.Secret),
            _ => throw new BadHttpRequestException(ApiErrorTestProgram.Secret, 409)
        };
    }

    [DisableDrnProblemDetails]
    [HttpGet("opt-out-action")]
    public IResult OptOutAction() => TypedResults.StatusCode(503);

    [HttpPost("validation")]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult Validation([FromBody] ProbeRequest request) => Ok(request);

    [HttpGet("typed-validation")]
    public IResult TypedValidation() => TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Name"] = ["Name is required."] });

    [HttpGet("problem")]
    public IResult ProblemResult() => TypedResults.Problem("Public message", statusCode: 401,
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_login" });

    [HttpGet("mvc-problem")]
    public IActionResult MvcProblem() => Problem("Public message", statusCode: 401);

    [HttpGet("challenge")]
    public IActionResult ChallengeResult() => Challenge(IdentityConstants.BearerScheme);

    [HttpGet("forbid")]
    public IActionResult ForbidResult() => Forbid(IdentityConstants.BearerScheme);

    [HttpGet("custom")]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest, "text/plain")]
    public IActionResult Custom() => new ContentResult { StatusCode = 400, ContentType = "text/plain", Content = "consumer body" };

    [HttpGet("pre-limit")]
    [HttpGet("post-limit")]
    public IActionResult Limited() => Ok();
}

public sealed class ProbeRequest
{
    [Required]
    public string? Name { get; init; }
}

[DisableDrnProblemDetails]
public abstract class ApiErrorOptOutControllerBase : ControllerBase;

[ApiController]
[AllowAnonymous]
[Route(ApiErrorTestProgram.Root + "/opt-out-controller")]
public sealed class ApiErrorOptOutController : ApiErrorOptOutControllerBase
{
    [HttpGet("status")]
    public IResult Status() => TypedResults.StatusCode(503);

    [HttpGet("mvc")]
    public IActionResult Mvc() => Problem("Public message", statusCode: 409);

    [HttpGet("typed")]
    public IResult Typed() => TypedResults.Problem("Public message", statusCode: 409);

    [HttpGet("throw")]
    public IActionResult Throw() => throw new BadHttpRequestException(ApiErrorTestProgram.Secret, 409);
}
