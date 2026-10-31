using DRN.Framework.SharedKernel.Attributes;

namespace DRN.Test.Integration.Tests.Sample.Controller.Helpers;

public class AuthenticatedUserModel
{
    [Personal] public string Email { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    [Sensitive] public string Password { get; init; } = string.Empty;
    [Sensitive] public string Token { get; init; } = string.Empty;
}
