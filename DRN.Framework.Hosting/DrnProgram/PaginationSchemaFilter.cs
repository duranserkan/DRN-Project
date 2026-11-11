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
        var description = GetDescription(context.MemberInfo?.DeclaringType, context.MemberInfo?.Name);
        if (description is null)
            return;

        if (schema is OpenApiSchema propertySchema)
            propertySchema.Description = description;
        else if (schema is OpenApiSchemaReference reference)
            reference.Description = description;
    }

    internal static string? GetDescription(Type? owner, string? member)
    {
        if (owner == typeof(PageCursor))
            return GetCursorDescription(member);

        if (owner is not null && typeof(PaginationResultBase).IsAssignableFrom(owner))
            return member switch
            {
                nameof(PaginationResultBase.Request) => "Request that produced this page. Its cursor describes the previous page, not this page's first and last items.",
                nameof(PaginationResultBase.FirstId) => "First item ID on this returned page. Use this outer ID when requesting another page.",
                nameof(PaginationResultBase.LastId) => "Last item ID on this returned page. Use this outer ID when requesting another page.",
                nameof(PaginationResultBase.ItemCount) => "Number of items returned on this page.",
                nameof(PaginationResultBase.HasNext) => "Whether another page exists in the current sort direction.",
                nameof(PaginationResultBase.HasPrevious) => "Whether a previous page exists in the current sort direction.",
                nameof(PaginationResultBase.Total) => "Total-count metadata. Count and Pages are -1 when unspecified.",
                nameof(PaginationResultBase.TotalCountUpdated) => "Whether this request asked to refresh the total count.",
                _ => null
            };

        if (owner == typeof(PageSize))
            return member switch
            {
                nameof(PageSize.Size) => "Requested page size. Defaults to 10, with a minimum of 1 and a ceiling of MaxSize.",
                nameof(PageSize.MaxSize) => "Page-size ceiling. Defaults to 100 and is capped at 1000. Endpoints may impose a lower ceiling.",
                _ => null
            };

        if (owner == typeof(PaginationTotal))
            return member switch
            {
                nameof(PaginationTotal.Count) => "Total matching items, or -1 when unspecified. Reused unless a count refresh is requested.",
                nameof(PaginationTotal.Pages) => "Total pages for the page size, or -1 when unspecified.",
                nameof(PaginationTotal.CountSpecified) => "Whether Count is known (zero or greater). Calculated from Count.",
                _ => null
            };

        if (owner == typeof(PaginationRequest))
            return member switch
            {
                nameof(PaginationRequest.PageNumber) => "Requested page number. Defaults to 1 and values below 1 become 1.",
                nameof(PaginationRequest.PageSize) => "Page-size settings. Omit the object to use the default size 10 and ceiling 100.",
                nameof(PaginationRequest.PageCursor) => "Previous-page cursor. Omit the object for an initial request.",
                nameof(PaginationRequest.TotalCount) => "Previously known total matching items, or -1 when unspecified.",
                nameof(PaginationRequest.UpdateTotalCount) => "Run a count query to refresh total metadata. Defaults to false.",
                nameof(PaginationRequest.MarkAsHasNextOnRefresh) => "Preserve the previous HasNext flag when refreshing a page.",
                _ => null
            };

        return null;
    }

    private static string? GetCursorDescription(string? member) => member switch
    {
        nameof(PageCursor.FirstId) => "First item ID from the previous page result. " + CursorUsage,
        nameof(PageCursor.LastId) => "Last item ID from the previous page result. " + CursorUsage,
        nameof(PageCursor.PageNumber) => "Page number of the previous result supplying the cursor IDs. Defaults to 1.",
        nameof(PageCursor.SortDirection) => "Ascending or Descending ID order. None is normalized to Ascending.",
        _ => null
    };
}
