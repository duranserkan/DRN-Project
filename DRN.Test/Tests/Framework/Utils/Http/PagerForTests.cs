using Sample.Hosted.Pages;

namespace DRN.Test.Tests.Framework.Utils.Http;

public class PagerForTests
{
    [Fact]
    public void PageFor_Should_Return_All_Pages()
    {
        var pages = PageFor.GetAllPages();
        var root = new RootPageFor();
        var user = new UserPageFor();
        var userManagement = new UserManagementPageFor();
        var systemManagement = new SystemManagementPageFor();

        pages.Should().Contain(root.GetPages());
        pages.Should().Contain(user.GetPages());
        pages.Should().Contain(userManagement.GetPages());
        pages.Should().Contain(systemManagement.GetPages());
    }
}