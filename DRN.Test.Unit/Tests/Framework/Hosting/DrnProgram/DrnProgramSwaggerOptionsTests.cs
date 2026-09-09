using DRN.Framework.Hosting.DrnProgram;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace DRN.Test.Unit.Tests.Framework.Hosting.DrnProgram;

public class DrnProgramSwaggerOptionsTests
{
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
