using Sample.Hosted.Helpers;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Endpoints;

public class PagerForTests
{
    [Fact]
    public void PageFor_Should_Return_All_Pages()
    {
        var pages = Get.Page.All;

        pages.Should().Contain(Get.Page.Root.GetPages());
        pages.Should().Contain(Get.Page.User.GetPages());
        pages.Should().Contain(Get.Page.User.Management.GetPages());
        pages.Should().Contain(Get.Page.System.GetPages());

        Get.Page.System.Setup.Should().Be("/System/Setup");
        // HomeAnonymous uses @page "/", but MVC still addresses it by page identity.
        Get.Page.Root.HomeAnonymous.Should().Be("/HomeAnonymous");
        Get.Page.Root.Root.Should().Be("/");
        Get.Page.Root.Swagger.Should().Be("/Swagger");
    }
}
