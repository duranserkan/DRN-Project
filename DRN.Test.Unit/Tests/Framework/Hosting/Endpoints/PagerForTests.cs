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
        pages.Should().Contain(Get.Page.UserManagement.GetPages());
        pages.Should().Contain(Get.Page.System.GetPages());

        Get.Page.UserManagement.ShowRecoveryCodes.Should().Be("/User/Management/ShowRecoveryCodes");
        Get.Page.System.Setup.Should().Be("/System/Setup");
    }
}