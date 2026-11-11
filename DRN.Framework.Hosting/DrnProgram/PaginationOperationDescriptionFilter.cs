using DRN.Framework.SharedKernel.Domain.Pagination;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DRN.Framework.Hosting.DrnProgram;

internal sealed class PaginationOperationDescriptionFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var descriptions = new List<string>();
        var hasBody = false;
        var inputs = context.ApiDescription.ParameterDescriptions
            .Select(parameter => (Type: parameter.ParameterDescriptor?.ParameterType ?? parameter.Type, parameter.Source))
            .Distinct();

        foreach (var (type, source) in inputs)
        {
            if (type != typeof(PaginationRequest) && type != typeof(PaginationResultInfo))
                continue;
            if (source != BindingSource.Query && source != BindingSource.Body)
                continue;

            var isResultInfo = type == typeof(PaginationResultInfo);
            if (source == BindingSource.Query)
                descriptions.Add(isResultInfo
                    ? "Pass the previous page's pagination metadata using the flattened query fields documented below."
                    : "Supply pagination settings using the flattened query fields documented below.");
            else
            {
                hasBody = true;
                descriptions.Add(isResultInfo
                    ? "Send the previous page's pagination metadata object as the request body, not the complete page response or only its nested request. Preserve the required fields shown in the body schema."
                    : "Send pagination settings as the request body. Preserve the required fields shown in the body schema.");
            }

            descriptions.Add(isResultInfo
                ? "The metadata's outer first and last IDs identify the returned page. Its nested request cursor describes the preceding page. Preserve that distinction when supplying continuation metadata."
                : "For continuation, set the target page number separately from the cursor's previous-page number. Use the previous result's outer first and last IDs for the cursor.");
        }

        if (hasBody && operation.Parameters is { Count: > 0 })
        {
            var queryNames = operation.Parameters.Where(parameter => parameter.In == ParameterLocation.Query)
                .Select(parameter => $"`{parameter.Name}`").ToArray();
            if (queryNames.Length > 0)
                descriptions.Add($"Keep {string.Join(", ", queryNames)} in the query string, outside the request body.");
        }

        var returnsPage = context.ApiDescription.SupportedResponseTypes.Any(response =>
            response.Type is { IsGenericType: true } type && type.GetGenericTypeDefinition() == typeof(PaginationResultModel<>));
        if (returnsPage)
            descriptions.Add("The response contains page items and pagination metadata. Use the metadata object when an endpoint accepts a previous page result. Its outer boundary IDs identify this returned page. The nested request cursor refers to the preceding page.");

        if (descriptions.Count == 0)
            return;
        if (!string.IsNullOrWhiteSpace(operation.Description))
            descriptions.Insert(0, operation.Description);
        operation.Description = string.Join("\n\n", descriptions);
    }
}
