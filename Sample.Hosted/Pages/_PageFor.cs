using DRN.Framework.Hosting.Endpoints;

namespace Sample.Hosted.Pages;

public abstract class PageFor : PageCollectionBase<PageFor>
{
    public static RootPageFor Root { get; } = new();
    public static TestPageFor Test { get; } = new();
    public static UserPageFor User { get; } = new();
    
    public static UseProfilePageFor UserProfile { get; } = new();
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

public class TestPageFor : PageForBase
{
    protected override string[] PathSegments { get; } = ["Test"];

    public string Htmx { get; init; } = string.Empty;
}


public class UserPageFor : PageForBase
{
    protected override string[] PathSegments { get; } = ["User"];

    public string Login { get; init; } = string.Empty;
    public string LoginWith2Fa { get; init; } = string.Empty;
    public string Logout { get; init; } = string.Empty;
    public string Lockout { get; init; } = string.Empty;
    public string Register { get; init; } = string.Empty;
}

public class UseProfilePageFor : PageForBase
{
    protected override string[] PathSegments { get; } = ["User", "Profile"];
    
    public string Details { get; init; } = string.Empty;
    public string Edit { get; init; } = string.Empty;
    public string Picture { get; init; } = string.Empty;
}

public class UserManagementPageFor : PageForBase
{
    protected override string[] PathSegments { get; } = ["User", "Management"];

    public string EnableAuthenticator { get; init; } = string.Empty;
    public string ShowRecoveryCodes { get; init; } = string.Empty;
}

public class SystemManagementPageFor : PageForBase
{
    protected override string[] PathSegments { get; } = ["System"];

    public string Setup { get; init; } = string.Empty;
}