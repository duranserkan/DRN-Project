using DRN.Framework.SharedKernel;

namespace DRN.Test.Tests.SharedKernel;

public class AppConstantTests
{
    [Fact]
    public void LocalIpAddress_Should_Be_Obtained()
    {
        AppConstants.LocalIpAddress.Should().NotBeNullOrWhiteSpace();
    }
}