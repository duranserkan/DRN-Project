using DRN.Framework.Hosting.Endpoints;

namespace Sample.Hosted.Helpers.PageFor;

public class UserPageFor : PageForBase
{
    protected override string[] PathSegments { get; } = ["User"];

    public string Login { get; init; } = string.Empty;
    public string LoginWith2Fa { get; init; } = string.Empty;
    public string Logout { get; init; } = string.Empty;
    public string Lockout { get; init; } = string.Empty;
    public string Register { get; init; } = string.Empty;
}

public class UserProfilePageFor : PageForBase
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