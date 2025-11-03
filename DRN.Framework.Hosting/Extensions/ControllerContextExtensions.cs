using Microsoft.AspNetCore.Mvc;

namespace DRN.Framework.Hosting.Extensions;

public static class ControllerContextExtensions
{
    /// <summary>
    /// Converts the ModelState into a ValidationProblemDetails instance.
    /// </summary>
    /// <param name="controller">The controller that owns model state.</param>
    /// <returns>A ValidationProblemDetails object populated with model state errors.</returns>
    public static ValidationProblemDetails GetValidationProblemDetails(this ControllerBase controller)
    {
        ArgumentNullException.ThrowIfNull(controller);
        var problemDetails = controller.ProblemDetailsFactory.CreateValidationProblemDetails(controller.HttpContext, controller.ModelState);

        return problemDetails;
    }
}