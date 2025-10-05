using DRN.Framework.Utils;

namespace DRN.Test.Unit.Tests.Framework.Utils;

public class UtilsModuleTests
{
    [Theory]
    [DataInlineUnit]
    public void AddDrnUtils_ShouldRegisterRequiredServices(TestContextUnit context)
    {
        context.ServiceCollection.AddDrnUtils();
        context.ValidateServices();

        var appSettings = context.GetRequiredService<IAppSettings>();
        var key = appSettings.NexusAppSettings.GetDefaultMacKey();
        key.Key.Should().Be("SFnefTwiLUfxc_RCX54vHROJQ50TDvqDdHImA2rvrso");
    }
}