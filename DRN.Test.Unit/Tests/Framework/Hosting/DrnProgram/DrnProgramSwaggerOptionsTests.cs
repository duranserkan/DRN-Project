using System.Globalization;
using System.Text.Json;
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.SharedKernel.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace DRN.Test.Unit.Tests.Framework.Hosting.DrnProgram;

public class DrnProgramSwaggerOptionsTests
{
    private sealed record Int64Response(long Value, long? OptionalValue);

    [Theory]
    [DataInlineUnit(42L, JsonValueKind.Number)]
    [DataInlineUnit(IntegerSafeIntervalForJs.Min, JsonValueKind.Number)]
    [DataInlineUnit(IntegerSafeIntervalForJs.Max, JsonValueKind.Number)]
    [DataInlineUnit(IntegerSafeIntervalForJs.Min - 1, JsonValueKind.String)]
    [DataInlineUnit(IntegerSafeIntervalForJs.Max + 1, JsonValueKind.String)]
    [DataInlineUnit(long.MinValue, JsonValueKind.String)]
    [DataInlineUnit(long.MaxValue, JsonValueKind.String)]
    public void Swagger_Int64_Properties_Should_Describe_Drn_Json(long value, JsonValueKind expectedKind)
    {
        var options = new SwaggerGenOptions();
        new DrnProgramSwaggerOptions().ConfigureSwaggerGenOptions(options);
        var generator = new SchemaGenerator(options.SchemaGeneratorOptions, new JsonSerializerDataContractResolver(JsonConventions.DefaultOptions));
        var repository = new SchemaRepository();
        generator.GenerateSchema(typeof(Int64Response), repository);
        var schema = repository.Schemas[nameof(Int64Response)];
        var json = JsonSerializer.SerializeToElement(new Int64Response(value, value), JsonConventions.DefaultOptions);

        foreach (var name in new[] { "value", "optionalValue" })
        {
            var alternatives = schema.Properties![name].OneOf!;
            var token = json.GetProperty(name);
            token.ValueKind.Should().Be(expectedKind);
            var numeric = alternatives.Single(s => s.Type == JsonSchemaType.Integer);
            numeric.Minimum.Should().Be(long.MinValue.ToString(CultureInfo.InvariantCulture));
            numeric.Maximum.Should().Be(long.MaxValue.ToString(CultureInfo.InvariantCulture));
            var quoted = alternatives.Single(s => s.Type == JsonSchemaType.String);
            quoted.Format.Should().Be("int64");
            if (quoted.Pattern is { } pattern)
                value.ToString(CultureInfo.InvariantCulture).Should().MatchRegex(pattern);
            alternatives.Any(s => s.Type == JsonSchemaType.Null).Should().Be(name == "optionalValue");
        }

        JsonSerializer.SerializeToElement(new Int64Response(value, null), JsonConventions.DefaultOptions)
            .GetProperty("optionalValue").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Theory]
    [DataInlineUnit("", "https://public.example:8443/")]
    [DataInlineUnit("/api", "https://public.example:8443/api")]
    public void Swagger_Server_Should_Use_Processed_Request_And_Ignore_Raw_Headers(string pathBase, string expected)
    {
        var options = new SwaggerEndpointOptions();
        new DrnProgramSwaggerOptions().ConfigureSwaggerEndpointOptions(options);
        var request = new DefaultHttpContext().Request;
        request.Scheme = "https";
        request.Host = new HostString("public.example", 8443);
        request.PathBase = pathBase;
        request.Path = "/custom-docs/v1/openapi.json";
        request.Headers["X-Forwarded-Host"] = "untrusted.example";
        request.Headers["X-Forwarded-Prefix"] = "/untrusted";
        var document = new OpenApiDocument();

        options.PreSerializeFilters.Single()(document, request);

        document.Servers.Should().ContainSingle().Which.Url.Should().Be(expected);
    }

    [Theory]
    [DataInlineUnit(null!)]
    [DataInlineUnit("")]
    [DataInlineUnit("/")]
    [DataInlineUnit("///")]
    public void Swagger_UI_Should_Reject_Empty_Or_Root_Prefixes(string? prefix)
    {
        var options = new DrnProgramSwaggerOptions
        {
            ConfigureSwaggerUIOptionsAction = ui => ui.RoutePrefix = prefix!
        };

        var configure = () => options.ConfigureSwaggerUI(new SwaggerUIOptions());

        configure.Should().Throw<ConfigurationException>().WithMessage("*RoutePrefix must be nonempty*");
        options.SwaggerUIPathPrefix.Should().BeNull();
    }

    [Theory]
    [DataInlineUnit("swagger", "swagger")]
    [DataInlineUnit("docs/Swagger", "docs/Swagger")]
    [DataInlineUnit("swagger-ui", "swagger-ui")]
    [DataInlineUnit("api-docs", "api-docs")]
    [DataInlineUnit("/docs", "docs")]
    [DataInlineUnit("docs/", "docs")]
    [DataInlineUnit("/docs/", "docs")]
    public void Swagger_UI_Should_Use_Validated_Prefix_After_One_Callback(string prefix, string expectedPrefix)
    {
        var calls = 0;
        var options = new DrnProgramSwaggerOptions
        {
            ConfigureSwaggerUIOptionsAction = ui =>
            {
                calls++;
                ui.RoutePrefix = prefix;
            }
        };

        var uiOptions = new SwaggerUIOptions();
        options.ConfigureSwaggerUI(uiOptions);

        calls.Should().Be(1);
        uiOptions.RoutePrefix.Should().Be(expectedPrefix);
        options.SwaggerUIPathPrefix!.Value.Value.Should().Be("/" + expectedPrefix);
    }
}
