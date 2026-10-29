using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DRN.Framework.Hosting.Middlewares.ApiErrors;

public sealed class DrnApiErrorOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        if (DrnApiEndpointMetadata.IsDisabled(metadata) ||
            !DrnApiEndpointMetadata.IsApi(metadata) ||
            string.Equals(context.ApiDescription.HttpMethod, "HEAD", StringComparison.OrdinalIgnoreCase))
            return;

        operation.Responses ??= new OpenApiResponses();
        var defaultType = context.ApiDescription.SupportedResponseTypes.FirstOrDefault(response => response.IsDefaultResponse)?.Type;
        var statuses = new[] { 400 }
            .Concat(context.ApiDescription.SupportedResponseTypes.Select(response => response.StatusCode))
            .Where(DrnApiErrorResponse.IsSupportedStatus).Distinct();

        foreach (var status in statuses)
        {
            var declared = context.ApiDescription.SupportedResponseTypes.FirstOrDefault(response => response.StatusCode == status);
            var type = declared?.Type ?? defaultType;
            if (type != null && type != typeof(void) && !typeof(ProblemDetails).IsAssignableFrom(type))
                continue;

            var key = status.ToString(CultureInfo.InvariantCulture);
            if (!operation.Responses.TryGetValue(key, out var existing))
                operation.Responses[key] = existing = new OpenApiResponse
                {
                    Description = TypedResults.Problem(statusCode: status).ProblemDetails.Title ?? "API error"
                };

            if (existing is not OpenApiResponse response)
                continue;

            // Preserve content supplied by another convention when no response type identifies it as a problem.
            if (type is null && response.Content is { Count: > 0 })
                continue;

            if (status == 400)
                response.Description = "Representative error response. API errors across all 4xx (400-499) and 5xx (500-599) " +
                    "statuses use Problem Details with the actual HTTP status and requestId. Validation includes errors. " +
                    "Explicit custom response bodies are preserved.";

            response.Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/problem+json"] = new()
                {
                    Schema = CreateSchema(context, type == null || type == typeof(void) ? typeof(ProblemDetails) : type)
                }
            };
        }
    }

    private static OpenApiSchema CreateSchema(OperationFilterContext context, Type type) => new()
    {
        AllOf = [context.SchemaGenerator.GenerateSchema(type, context.SchemaRepository)],
        Type = JsonSchemaType.Object,
        Required = new HashSet<string> { "type", "title", "status", "instance", "requestId" },
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["requestId"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Description = "Request reference matching the server's scoped logs."
            }
        }
    };
}
