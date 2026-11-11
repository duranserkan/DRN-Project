using DRN.Framework.SharedKernel.Domain.Pagination;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DRN.Framework.Hosting.DrnProgram;

internal sealed class PaginationQueryOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.Parameters is not { Count: > 0 } parameters)
            return;

        foreach (var description in context.ApiDescription.ParameterDescriptions)
        {
            if (description.Source != BindingSource.Query || description.ParameterDescriptor?.ParameterType != typeof(PaginationRequest))
                continue;

            var query = parameters.OfType<OpenApiParameter>().FirstOrDefault(p =>
                p.In == ParameterLocation.Query && string.Equals(p.Name, description.Name, StringComparison.OrdinalIgnoreCase));
            if (query is null)
                continue;

            var metadata = description.ModelMetadata;
            var container = metadata?.ContainerType;
            if (container == typeof(PaginationTotal) || metadata?.IsReadOnly == true)
                parameters.Remove(query);
            else if (container == typeof(PageSize) || container == typeof(PageCursor))
            {
                query.Required = false;
                if (container == typeof(PageCursor) && metadata?.PropertyName is nameof(PageCursor.FirstId) or nameof(PageCursor.LastId))
                    query.Description = "Optional on the first request. When PageCursor.PageNumber exceeds 1 or PageCursor.LastId is a nonzero UUID, " +
                        "both IDs are required. Copy FirstId and LastId from the previous page result.";
            }
        }
    }
}
