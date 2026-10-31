using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Sample.Hosted.Pages.Test;

namespace DRN.Test.Unit.Tests.Sample.Pages;

public class HtmxTests
{
    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public async Task Manual_Csrf_Validation_Should_Keep_Success_And_Use_Safe_Error_Defaults(
        DrnTestContextUnit services, bool invalidToken)
    {
        ScopeContext.InitializeForTest(services);
        var http = new DefaultHttpContext();
        var antiforgery = Substitute.For<IAntiforgery>();
        antiforgery.ValidateRequestAsync(http).Returns(invalidToken
            ? Task.FromException(new AntiforgeryValidationException("private-token-diagnostic"))
            : Task.CompletedTask);
        var metadataProvider = new EmptyModelMetadataProvider();
        var page = new Htmx(antiforgery)
        {
            MetadataProvider = metadataProvider,
            PageContext = new PageContext
            {
                HttpContext = http,
                ViewData = new ViewDataDictionary(metadataProvider, new ModelStateDictionary())
            }
        };

        var result = await page.OnGetExplicitValidationAsync("Original message");

        _ = antiforgery.Received(1).ValidateRequestAsync(http);
        if (invalidToken)
        {
            result.Should().BeOfType<BadRequestResult>().Which.StatusCode.Should().Be(400);
            ScopeContext.Problem.Detail.Should().Be("The request could not be verified. Refresh the page and try again.")
                .And.NotContain("private-token-diagnostic");
            return;
        }

        var partial = result.Should().BeOfType<PartialViewResult>().Which;
        partial.ViewName.Should().Be("_ResultMessage");
        partial.ViewData!.Model.Should().BeEquivalentTo(new
        {
            Message = "Original message",
            TokenValidation = "Success (Explicit Validation)"
        });
        ScopeContext.CurrentProblem.Should().BeNull();
    }
}
