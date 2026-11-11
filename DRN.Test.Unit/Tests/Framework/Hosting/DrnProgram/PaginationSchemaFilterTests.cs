using System.Text.Json;
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.SharedKernel.Json;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DRN.Test.Unit.Tests.Framework.Hosting.DrnProgram;

public class PaginationSchemaFilterTests
{
    private sealed record UnrelatedCursor(Guid FirstId, Guid LastId);

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public void Json_Pagination_Schemas_Should_Describe_Cursors_And_Preserve_Required_Fields(bool camelCase)
    {
        var jsonOptions = JsonConventions.SetJsonDefaults();
        jsonOptions.PropertyNamingPolicy = camelCase ? JsonNamingPolicy.CamelCase : null;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers().AddJsonOptions(options =>
        {
            JsonConventions.SetJsonDefaults(options.JsonSerializerOptions);
            options.JsonSerializerOptions.PropertyNamingPolicy = jsonOptions.PropertyNamingPolicy;
        });
        services.AddSwaggerGen(options => new DrnProgramSwaggerOptions().ConfigureSwaggerGenOptions(options));
        using var provider = services.BuildServiceProvider();
        var generator = provider.GetRequiredService<ISchemaGenerator>();
        var repository = new SchemaRepository();
        generator.GenerateSchema(typeof(PaginationRequest), repository);
        generator.GenerateSchema(typeof(PaginationResultInfo), repository);
        generator.GenerateSchema(typeof(UnrelatedCursor), repository);

        var baseline = new SchemaRepository();
        var baselineGenerator = new SchemaGenerator(new SchemaGeneratorOptions(), new JsonSerializerDataContractResolver(jsonOptions));
        baselineGenerator.GenerateSchema(typeof(PaginationRequest), baseline);
        baselineGenerator.GenerateSchema(typeof(PaginationResultInfo), baseline);

        foreach (var type in new[] { typeof(PaginationRequest), typeof(PageSize), typeof(PageCursor), typeof(PaginationResultInfo), typeof(PaginationTotal) })
        {
            var schema = repository.Schemas[type.Name];
            schema.Required.Should().BeEquivalentTo(baseline.Schemas[type.Name].Required);
            schema.Properties!.Keys.Should().BeEquivalentTo(baseline.Schemas[type.Name].Properties!.Keys);
        }

        string PropertyName(string name) => jsonOptions.PropertyNamingPolicy?.ConvertName(name) ?? name;
        var cursor = repository.Schemas[nameof(PageCursor)];
        cursor.Required.Should().BeEquivalentTo(new[]
        {
            PropertyName(nameof(PageCursor.PageNumber)), PropertyName(nameof(PageCursor.SortDirection)),
            PropertyName(nameof(PageCursor.FirstId)), PropertyName(nameof(PageCursor.LastId))
        });
        repository.Schemas[nameof(PageSize)].Required.Should().BeEquivalentTo(new[]
        {
            PropertyName(nameof(PageSize.Size)), PropertyName(nameof(PageSize.MaxSize))
        });
        repository.Schemas[nameof(PaginationRequest)].Properties.Should().NotContainKey(PropertyName(nameof(PaginationRequest.Total)));
        cursor.Properties.Should().NotContainKey(PropertyName(nameof(PageCursor.IsFirstRequest)));

        foreach (var name in new[] { nameof(PageCursor.FirstId), nameof(PageCursor.LastId) })
        {
            var property = cursor.Properties![PropertyName(name)];
            property.Description.Should().Contain("zero UUID").And.Contain("both IDs from the previous page result");
            property.Format.Should().Be("uuid");
            repository.Schemas[nameof(UnrelatedCursor)].Properties![PropertyName(name)].Description.Should().BeNull();
        }

        cursor.Properties![PropertyName(nameof(PageCursor.FirstId))].Description.Should().StartWith("First item ID");
        cursor.Properties[PropertyName(nameof(PageCursor.LastId))].Description.Should().StartWith("Last item ID");

        var info = repository.Schemas[nameof(PaginationResultInfo)];
        info.Properties![PropertyName(nameof(PaginationResultInfo.Request))].Description.Should().Contain("previous page");
        info.Properties[PropertyName(nameof(PaginationResultInfo.Total))].Description.Should().Contain("-1 when unspecified");
        foreach (var name in new[] { nameof(PaginationResultInfo.FirstId), nameof(PaginationResultInfo.LastId) })
        {
            info.Required.Should().Contain(PropertyName(name));
            info.Properties![PropertyName(name)].Description.Should().Contain("this returned page").And.Contain("outer ID");
        }
        repository.Schemas[nameof(PaginationTotal)].Properties![PropertyName(nameof(PaginationTotal.Count))]
            .Description.Should().Contain("-1 when unspecified");
    }
}
