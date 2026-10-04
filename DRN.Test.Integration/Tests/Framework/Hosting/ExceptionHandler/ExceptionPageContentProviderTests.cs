using DRN.Framework.Hosting.Middlewares.ExceptionHandler;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils;
using DRN.Test.Utils.Hosting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace DRN.Test.Integration.Tests.Framework.Hosting.ExceptionHandler;

public class ExceptionPageContentProviderTests
{
    [Theory]
    [DataInline("", false)]
    [DataInline("/gateway", false)]
    [DataInline("", true)]
    [DataInline("/gateway", true)]
    public async Task Error_Pages_Should_Render_Assets_With_The_Parent_PathBase_And_Nonce(
        DrnTestContext context, string pathBase, bool compilation)
    {
        var application = await context.ApplicationContext.CreateApplicationAndBindDependenciesAsync<ForwardedHeadersTestProgram>();
        using var scope = application.Services.CreateScope();
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        http.Request.PathBase = pathBase;
        http.Request.Path = "/failing-page";
        http.Items["NETESCAPADES_NONCE"] = "error-page-test-nonce";
        Exception exception = compilation ? new TestCompilationException() : new InvalidOperationException("Test failure");
        var model = await scope.ServiceProvider.GetRequiredService<IExceptionUtils>().CreateErrorPageModelAsync(http, exception);

        var result = await scope.ServiceProvider.GetRequiredService<IExceptionPageContentProvider>()
            .CreateErrorContentResult(http, exception, model);

        result.ContentType.Should().Be("text/html; charset=utf-8");
        result.Content.Should().Contain($"href=\"{pathBase}/_content/DRN.Framework.Hosting/css/error-page.css\"")
            .And.Contain($"src=\"{pathBase}/_content/DRN.Framework.Hosting/js/error-page.js\"")
            .And.Contain("Nonce=\"error-page-test-nonce\"")
            .And.NotContain("~/_content/");
        if (!compilation)
            result.Content.Should().Contain($"src=\"{pathBase}/_content/DRN.Framework.Hosting/jsoneditor/json-editor.js\"");
    }

    private sealed class TestCompilationException : Exception, ICompilationException
    {
        public IEnumerable<CompilationFailure> CompilationFailures => [];
    }
}
