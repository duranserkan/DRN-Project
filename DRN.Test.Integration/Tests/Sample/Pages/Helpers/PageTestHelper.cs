using DRN.Framework.Testing.Providers;
using Microsoft.AspNetCore.Identity;
using Sample.Domain.Users;

namespace DRN.Test.Integration.Tests.Sample.Pages.Helpers;

internal static class PageTestHelper
{
    public static async Task<(SampleUser User, TestUserCredentials Credentials, string AuthenticatorKey)> CreateMfaUserAsync(
        UserManager<SampleUser> manager)
    {
        var credentials = CredentialsProvider.GenerateCredentials();
        var user = new SampleUser
        {
            UserName = credentials.EmailAddress, Email = credentials.EmailAddress,
            EmailConfirmed = true, PhoneNumberConfirmed = true
        };
        (await manager.CreateAsync(user, credentials.Password)).Succeeded.Should().BeTrue();
        (await manager.ResetAuthenticatorKeyAsync(user)).Succeeded.Should().BeTrue();
        (await manager.SetTwoFactorEnabledAsync(user, true)).Succeeded.Should().BeTrue();
        var key = await manager.GetAuthenticatorKeyAsync(user);
        key.Should().NotBeNullOrWhiteSpace();

        return (user, credentials, key!);
    }

    public static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string pagePath)
    {
        var html = await client.GetStringAsync(pagePath);
        const string tokenName = "__RequestVerificationToken";
        var nameIndex = html.IndexOf($"name=\"{tokenName}\"", StringComparison.Ordinal);
        nameIndex.Should().BeGreaterThanOrEqualTo(0);

        const string valuePrefix = "value=\"";
        var valueIndex = html.IndexOf(valuePrefix, nameIndex, StringComparison.Ordinal);
        valueIndex.Should().BeGreaterThanOrEqualTo(0);
        valueIndex += valuePrefix.Length;

        var valueEndIndex = html.IndexOf('"', valueIndex);
        valueEndIndex.Should().BeGreaterThan(valueIndex);

        var token = html[valueIndex..valueEndIndex];
        token.Should().NotBeNullOrWhiteSpace();

        return token;
    }
}
