using DRN.Framework.SharedKernel;
using AwesomeAssertions;
using Xunit;

namespace DRN.Test.Unit.Tests.Framework.SharedKernel;

public class AppConstantTests
{
    [Fact]
    public void LocalIpAddress_Should_Be_Obtained()
    {
        AppConstants.LocalIpAddress.Should().NotBeNullOrWhiteSpace();
    }
}