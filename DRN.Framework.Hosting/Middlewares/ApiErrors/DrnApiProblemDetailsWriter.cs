using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace DRN.Framework.Hosting.Middlewares.ApiErrors;

// Register before AddProblemDetails so API errors do not depend on Accept negotiation.
[Singleton<IProblemDetailsWriter>(tryAdd: false)]
public sealed class DrnApiProblemDetailsWriter(DrnApiErrorResponse response, IOptions<ProblemDetailsOptions> options) : IProblemDetailsWriter
{
    public bool CanWrite(ProblemDetailsContext context) =>
        response.CanHandle(context.HttpContext, context.ProblemDetails.Status);

    public ValueTask WriteAsync(ProblemDetailsContext context)
    {
        options.Value.CustomizeProblemDetails?.Invoke(context);
        return new(response.WriteAsync(context.HttpContext, context.ProblemDetails));
    }
}
