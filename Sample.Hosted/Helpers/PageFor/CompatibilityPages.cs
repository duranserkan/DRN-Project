using DRN.Framework.SharedKernel.Attributes;

namespace Sample.Hosted.Helpers.PageFor;

public partial class RootPageFor
{
    // Compatibility destinations are not additional Razor pages.
    public string Root { get; init; } = "/";
    public string Swagger { get; init; } = "/Swagger";
}

public partial class UserManagementPageFor
{
    [Sensitive(false, Justification = "Page identity, not recovery codes.")]
    public string ShowRecoveryCodes { get; init; } = "/User/Management/ShowRecoveryCodes";
}
