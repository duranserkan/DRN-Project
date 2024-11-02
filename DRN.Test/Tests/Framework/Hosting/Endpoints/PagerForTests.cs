using Sample.Hosted.Pages;

namespace DRN.Test.Tests.Framework.Hosting.Endpoints;

public class PagerForTests
{
    [Fact]
    public void PageFor_Should_Return_All_Pages()
    {
        var pages = PageFor.GetAllPages();

        pages.Should().Contain(PageFor.Root.GetPages());
        pages.Should().Contain(PageFor.User.GetPages());
        pages.Should().Contain(PageFor.UserManagement.GetPages());
        pages.Should().Contain(PageFor.SystemManagement.GetPages());

        PageFor.UserManagement.ShowRecoveryCodes.Should().Be("/User/Management/ShowRecoveryCodes");
        PageFor.SystemManagement.Setup.Should().Be("/System/Setup");
    }
}