using AwesomeAssertions;
using DRN.Framework.SharedKernel.Domain;

namespace DRN.Test.Unit.Tests.Framework.SharedKernel.Domain;

public class AppIdTests
{
    [Fact]
    public void IAppId_Constants_Should_Match_Expected_Values()
    {
        IAppId.DefaultAppId.Should().Be(0);
        IAppId.NexusAppId.Should().Be(126);
        IAppId.TestAppId.Should().Be(127);
        IAppId.MaxAppId.Should().Be(127);
    }

    [Fact]
    public void BuiltInApps_Should_Expose_Consistent_Value_And_AppId()
    {
        DefaultApp.Value.Should().Be(IAppId.DefaultAppId);
        DefaultApp.AppId.Should().Be(IAppId.DefaultAppId);

        NexusApp.Value.Should().Be(IAppId.NexusAppId);
        NexusApp.AppId.Should().Be(IAppId.NexusAppId);

        TestApp.Value.Should().Be(IAppId.TestAppId);
        TestApp.AppId.Should().Be(IAppId.TestAppId);
    }

    [Fact]
    public void Generic_AppId_Resolution_Should_Return_Struct_AppId()
    {
        GetAppId<DefaultApp>().Should().Be(0);
        GetAppId<NexusApp>().Should().Be(126);
        GetAppId<TestApp>().Should().Be(127);
    }

    private static byte GetAppId<TApp>() where TApp : IAppId => TApp.AppId;
}
