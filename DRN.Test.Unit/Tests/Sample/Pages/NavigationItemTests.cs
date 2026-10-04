using Sample.Hosted.Helpers;
using Sample.Hosted.Pages.Shared.Models;
using Sample.Hosted.Pages.User.Profile.Models;

namespace DRN.Test.Unit.Tests.Sample.Pages;

public class NavigationItemTests
{
    [Fact]
    public void Page_Navigation_Should_Retain_Page_Identifiers_Without_Url_Syntax()
    {
        var items = new ProfileSubNavigationCollection().Items;

        items.Select(item => item.PageName).Should().Equal(
            Get.Page.User.Profile.Details, Get.Page.User.Profile.Edit, Get.Page.User.Profile.Picture);
        new SidebarNavigationCollection().Items[0].PageName.Should().Be(Get.Page.Root.Home);
        new SidebarSettingsCollection().Items.Single(item => item.Title == "My Profile")
            .PageName.Should().Be(Get.Page.User.Profile.Details);
    }

    [Theory]
    [DataInlineUnit("https://example.com/profile")]
    [DataInlineUnit("#section")]
    [DataInlineUnit("~/assets/help.html")]
    [DataInlineUnit("/gateway/custom")]
    public void Url_Navigation_Should_Remain_Distinct_From_Page_Navigation(string href)
    {
        var sub = SubNavigationItem.ForUrl(href, "Link");
        var sidebar = SidebarNavigationItem.ForUrl(href, "Link", "bi-link");
        var settings = SidebarSettingsItem.ForUrl(href, "Link");

        sub.PageName.Should().BeNull();
        sidebar.PageName.Should().BeNull();
        settings.PageName.Should().BeNull();
        sub.Href.Should().Be(href);
        sidebar.Href.Should().Be(href);
        settings.Href.Should().Be(href);
        new SubNavigationItem(href, "Legacy").PageName.Should().BeNull();
        new SidebarNavigationItem(href, "Legacy", "bi-link").Href.Should().Be(href);
        new SidebarSettingsItem("Legacy", href).Href.Should().Be(href);
        new SidebarSettingsItem(1).Divider.Should().BeTrue();
    }
}
