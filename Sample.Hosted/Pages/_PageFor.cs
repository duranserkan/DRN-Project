using DRN.Framework.Hosting.Endpoints;

namespace Sample.Hosted.Pages;

public abstract class PageFor : PageCollectionBase<PageFor>
{
    public static RootPageFor Root { get; } = new();
    public static UserPageFor User { get; } = new();
    public static UserManagementPageFor UserManagement { get; } = new();
    public static SystemManagementPageFor SystemManagement { get; } = new();
}

public class RootPageFor : PageForBase
{
    protected override string[] PathSegments { get; } = [];

    public string Root { get; init; } = "/";
    public string Home { get; init; } = string.Empty;
    public string Swagger { get; init; } = string.Empty;
}

public class UserPageFor : PageForBase
{
    protected override string[] PathSegments { get; } = ["User"];

    public string Login { get; init; } = string.Empty;
    public string LoginWith2Fa { get; init; } = string.Empty;
    public string Logout { get; init; } = string.Empty;
    public string Profile { get; init; } = string.Empty;
    public string ProfileEdit { get; init; } = string.Empty;
    public string ProfilePicture { get; init; } = string.Empty;
    public string Register { get; init; } = string.Empty;
}

public class UserManagementPageFor : PageForBase
{
    protected override string[] PathSegments { get; } = ["User", "Manage"];

    public string EnableAuthenticator { get; init; } = string.Empty;
    public string ShowRecoveryCodes { get; init; } = string.Empty;
}

public class SystemManagementPageFor : PageForBase
{
    protected override string[] PathSegments { get; } = ["System"];

    public string Setup { get; init; } = string.Empty;
}