namespace DRN.Test.Tests.Framework.Testing.Providers;

public class CredentialsProviderTests
{
    [Fact]
    public void CredentialsProvider_Should_Return_UniqueCredentials()
    {
        var credentials = CredentialsProvider.Credentials;

        credentials.Should().NotBeNull();

        credentials.EmailAddress.Should().NotBeEmpty();
        credentials.Username.Should().NotBeNullOrEmpty();
        credentials.Password.Should().NotBeNullOrEmpty();

        credentials.Password.Should().Contain(CredentialsProvider.Unique1.ToString(), Exactly.Once());
        credentials.Password.Should().Contain(CredentialsProvider.Unique2.ToString(), Exactly.Once());
        credentials.Password.Should().ContainAny(CredentialsProvider.Digits.ToCharArray().Select(c => c.ToString()));
        credentials.Password.Should().ContainAny(CredentialsProvider.Lowercase.ToCharArray().Select(c => c.ToString()));
        credentials.Password.Should().ContainAny(CredentialsProvider.Uppercase.ToCharArray().Select(c => c.ToString()));
        credentials.Password.Should().ContainAny(CredentialsProvider.Special.ToCharArray().Select(c => c.ToString()));
    }
}