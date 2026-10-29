using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DRN.Framework.Hosting.Middlewares.ApiErrors;

[Transient<DrnApiProblemDetailsFilter>]
public sealed class DrnApiProblemDetailsFilter(DrnApiErrorResponse response) : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not ObjectResult { Value: ProblemDetails problem } result)
            return;

        var status = result.StatusCode ?? problem.Status ?? context.HttpContext.Response.StatusCode;
        if (!response.CanHandle(context.HttpContext, status))
            return;

        problem.Status = status;
        context.Result = new ApiProblemResult(problem);
    }

    public void OnResultExecuted(ResultExecutedContext context) { }

    private sealed class ApiProblemResult(ProblemDetails problem) : IActionResult
    {
        public Task ExecuteResultAsync(ActionContext context) => DrnApiErrorResponse.WriteAsync(context.HttpContext, problem);
    }
}
