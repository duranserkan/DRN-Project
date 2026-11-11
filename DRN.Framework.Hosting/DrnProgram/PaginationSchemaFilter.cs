using DRN.Framework.SharedKernel.Domain.Pagination;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DRN.Framework.Hosting.DrnProgram;

internal sealed class PaginationSchemaFilter : ISchemaFilter
{
    private const string CursorUsage = "Use the zero UUID for an initial cursor. When PageNumber exceeds 1 or LastId is a nonzero UUID, " +
        "supply both IDs from the previous page result.";

    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema propertySchema || context.MemberInfo?.DeclaringType != typeof(PageCursor))
            return;

        if (context.MemberInfo.Name == nameof(PageCursor.FirstId))
            propertySchema.Description = "First item ID from the previous page result. " + CursorUsage;
        else if (context.MemberInfo.Name == nameof(PageCursor.LastId))
            propertySchema.Description = "Last item ID from the previous page result. " + CursorUsage;
    }
}
