using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.SharedKernel.Domain.Pagination;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.OpenApi;
using NSubstitute;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DRN.Test.Unit.Tests.Framework.Hosting.DrnProgram;

public class PaginationQueryOperationFilterTests
{
    [Theory]
    [DataInlineUnit(null, "FirstId", "FirstId")]
    [DataInlineUnit("previous", "FirstId", "previous.FirstId")]
    [DataInlineUnit("previous", "Request.PageNumber", "previous.Request.PageNumber")]
    [DataInlineUnit("previous", "previous.FirstId", "previous.FirstId")]
    public void Query_Fields_Should_Preserve_Explicit_Model_Prefixes(string? prefix, string name, string expected)
    {
        var description = new ApiDescription();
        description.ParameterDescriptions.Add(new ApiParameterDescription
        {
            Name = name,
            Source = BindingSource.Query,
            ParameterDescriptor = new ParameterDescriptor
            {
                Name = "info",
                ParameterType = typeof(PaginationResultInfo),
                BindingInfo = new BindingInfo { BinderModelName = prefix, BindingSource = BindingSource.Query }
            }
        });
        var query = new OpenApiParameter { Name = name, In = ParameterLocation.Query };
        var operation = new OpenApiOperation { Parameters = [query] };
        var context = new OperationFilterContext(description, Substitute.For<ISchemaGenerator>(), new SchemaRepository(),
            new OpenApiDocument(),
            typeof(PaginationQueryOperationFilterTests).GetMethod(nameof(Query_Fields_Should_Preserve_Explicit_Model_Prefixes))!);

        new PaginationQueryOperationFilter().Apply(operation, context);

        query.Name.Should().Be(expected);
        description.ParameterDescriptions.Single().Name.Should().Be(name);
    }
}
