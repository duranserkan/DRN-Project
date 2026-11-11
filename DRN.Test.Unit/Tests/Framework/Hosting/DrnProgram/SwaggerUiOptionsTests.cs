using DRN.Framework.Hosting;
using DRN.Framework.Hosting.DrnProgram;
using Microsoft.AspNetCore.Builder;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace DRN.Test.Unit.Tests.Framework.Hosting.DrnProgram;

public class SwaggerUiOptionsTests
{
    [Theory]
    [DataInlineUnit(true)]
    [DataInlineUnit(false)]
    public void Metadata_Should_Run_Once_Before_UI_And_Document_Configuration(bool generatorFirst)
    {
        var calls = 0;
        var options = new DrnProgramSwaggerOptions
        {
            ConfigureOpenApiInfo = info =>
            {
                calls++;
                info.Title = "Configured API";
                info.Version = "v2";
            }
        };
        var generator = new SwaggerGenOptions();
        if (generatorFirst) options.ConfigureSwaggerGenOptions(generator);
        var ui = new SwaggerUIOptions();
        var defaultCacheLifetime = ui.CacheLifetime;
        options.ConfigureSwaggerUI(ui);
        if (!generatorFirst) options.ConfigureSwaggerGenOptions(generator);

        calls.Should().Be(1);
        ui.DocumentTitle.Should().Be("Configured API (v2)");
        ui.CacheLifetime.Should().Be(defaultCacheLifetime);
        generator.SwaggerGeneratorOptions.SwaggerDocs["v2"].Title.Should().Be("Configured API");
    }

    [Fact]
    public void UI_Callback_Should_Override_Defaults_And_Preserve_Explicit_Document_Url()
    {
        var calls = 0;
        var options = new DrnProgramSwaggerOptions
        {
            Title = "API title",
            ConfigureSwaggerUIOptionsAction = ui =>
            {
                calls++;
                ui.DocumentTitle.Should().Be("API title");
                ui.CacheLifetime.Should().Be(new SwaggerUIOptions().CacheLifetime);
                ui.DocumentTitle = "Custom title";
                ui.CacheLifetime = TimeSpan.FromMinutes(5);
                ui.SwaggerEndpoint("../custom.json", "Custom");
                ui.RoutePrefix = "docs/swagger";
                ui.HeadContent = "<meta name=\"custom\" content=\"preserved\">";
            }
        };
        var ui = new SwaggerUIOptions();
        options.ConfigureSwaggerUI(ui, new DrnPageOptions { FaviconUrl = "/images/icon.svg" });

        OpenApiDocumentEndpoints.ConfigureSwaggerDocumentUrl(ui, options).Should().BeFalse();
        calls.Should().Be(1);
        ui.DocumentTitle.Should().Be("Custom title");
        ui.CacheLifetime.Should().Be(TimeSpan.FromMinutes(5));
        ui.ConfigObject.Urls.Should().ContainSingle().Which.Url.Should().Be("../custom.json");
        ui.HeadContent.Should().Be("<meta name=\"custom\" content=\"preserved\"><link rel=\"icon\" href=\"../../images/icon.svg\">");
    }

    [Theory]
    [DataInlineUnit("docs//swagger")]
    [DataInlineUnit("../swagger")]
    [DataInlineUnit("{route}")]
    [DataInlineUnit("swagger?x=1")]
    public void UI_Should_Reject_Invalid_Path_Segments(string prefix)
    {
        var options = new DrnProgramSwaggerOptions
        {
            ConfigureSwaggerUIOptionsAction = ui => ui.RoutePrefix = prefix
        };
        var configure = () => options.ConfigureSwaggerUI(new SwaggerUIOptions());
        configure.Should().Throw<ConfigurationException>();
    }
}
