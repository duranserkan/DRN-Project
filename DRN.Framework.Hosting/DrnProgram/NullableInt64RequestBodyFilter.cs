using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DRN.Framework.Hosting.DrnProgram;

internal sealed class NullableInt64RequestBodyFilter : IRequestBodyFilter
{
    public void Apply(IOpenApiRequestBody requestBody, RequestBodyFilterContext context)
    {
        if (context.BodyParameterDescription?.Type != typeof(long?) ||
            context.BodyParameterDescription.ModelMetadata?.IsRequired == true || requestBody.Content is null)
            return;

        foreach (var content in requestBody.Content.Values)
        {
            if (content.Schema is OpenApiSchema { OneOf: { Count: > 0 } alternatives } &&
                alternatives.All(schema => schema.Type != JsonSchemaType.Null))
                alternatives.Add(new OpenApiSchema { Type = JsonSchemaType.Null });
        }
    }
}
