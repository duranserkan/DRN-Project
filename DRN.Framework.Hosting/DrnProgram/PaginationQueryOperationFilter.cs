using DRN.Framework.SharedKernel.Domain.Pagination;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
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
            var parameterType = description.ParameterDescriptor?.ParameterType;
            var isResultInfo = parameterType == typeof(PaginationResultInfo);
            if (description.Source != BindingSource.Query || (parameterType != typeof(PaginationRequest) && !isResultInfo))
                continue;

            var query = parameters.OfType<OpenApiParameter>().FirstOrDefault(p =>
                p.In == ParameterLocation.Query && string.Equals(p.Name, description.Name, StringComparison.OrdinalIgnoreCase));
            if (query is null)
                continue;

            var metadata = description.ModelMetadata;
            var container = metadata?.ContainerType;
            var isCalculatedTotal = container == typeof(PaginationTotal) &&
                (!isResultInfo || description.Name.Contains("Request.Total.", StringComparison.OrdinalIgnoreCase));
            if (isCalculatedTotal || metadata?.IsReadOnly == true)
            {
                parameters.Remove(query);
                continue;
            }

            DescribeQueryField(query, description);
            ApplyQueryDefaults(query, metadata, isResultInfo);
        }
    }

    private static void DescribeQueryField(OpenApiParameter query, ApiParameterDescription description)
    {
        // API Explorer can omit an explicit model prefix when flattening complex query parameters.
        var prefix = description.ParameterDescriptor?.BindingInfo?.BinderModelName;
        if (!string.IsNullOrEmpty(prefix) &&
            !description.Name.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase))
            query.Name = ModelNames.CreatePropertyModelName(prefix, description.Name);

        var metadata = description.ModelMetadata;
        var fieldDescription = PaginationSchemaFilter.GetDescription(metadata?.ContainerType, metadata?.PropertyName);
        if (fieldDescription is not null)
            query.Description = fieldDescription;
    }

    private static void ApplyQueryDefaults(OpenApiParameter query, ModelMetadata? metadata, bool isResultInfo)
    {
        if (metadata is null)
            return;

        var container = metadata.ContainerType;
        var isResultValue = isResultInfo && container is not null &&
            typeof(PaginationResultBase).IsAssignableFrom(container) && metadata.ModelType.IsValueType;
        var hasQueryDefault = container == typeof(PageSize) || container == typeof(PageCursor) ||
            (isResultInfo && container == typeof(PaginationTotal)) || isResultValue;
        if (!hasQueryDefault)
            return;

        query.Required = false;
        if (container == typeof(PageCursor) && metadata.PropertyName is nameof(PageCursor.FirstId) or nameof(PageCursor.LastId))
            query.Description = "Optional on the first request. When PageCursor.PageNumber exceeds 1 or PageCursor.LastId is a nonzero UUID, " +
                "both IDs are required. Copy FirstId and LastId from the previous page result.";
    }
}
