namespace DRN.Test.Tests.Framework.SharedKernel;

public class AppConstantTests
{
    [Fact]
    public void LocalIpAddress_Should_Be_Obtained()
    {
        AppConstants.LocalIpAddress.Should().NotBeNullOrWhiteSpace();
    }
}