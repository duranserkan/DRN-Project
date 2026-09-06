using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.SharedKernel;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace DRN.Test.Unit.Tests.Framework.Hosting.DrnProgram;

public class DrnProgramSwaggerOptionsTests
{
    [Theory]
    [DataInlineUnit(null)]
    [DataInlineUnit("")]
    [DataInlineUnit("/")]
    [DataInlineUnit("///")]
    public void Swagger_UI_Should_Reject_Empty_Or_Root_Prefixes(string? prefix)
    {
        var options = new DrnProgramSwaggerOptions
        {
            ConfigureSwaggerUIOptionsAction = ui => ui.RoutePrefix = prefix!
        };

        Action configure = () => options.ConfigureSwaggerUI(new SwaggerUIOptions());

        configure.Should().Throw<ConfigurationException>().WithMessage("*RoutePrefix must be nonempty*");
        options.SwaggerUIPathPrefix.Should().BeNull();
    }

    [Theory]
    [DataInlineUnit("swagger")]
    [DataInlineUnit("docs/Swagger")]
    [DataInlineUnit("swagger-ui")]
    [DataInlineUnit("api-docs")]
    public void Swagger_UI_Should_Use_Validated_Prefix_After_One_Callback(string prefix)
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

        options.ConfigureSwaggerUI(new SwaggerUIOptions());

        calls.Should().Be(1);
        options.SwaggerUIPathPrefix!.Value.Value.Should().Be("/" + prefix);
    }
}
