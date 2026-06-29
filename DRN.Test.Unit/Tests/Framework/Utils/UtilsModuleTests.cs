using DRN.Framework.Utils;

namespace DRN.Test.Unit.Tests.Framework.Utils;

public class UtilsModuleTests
{
    [Theory]
    [DataInlineUnit]
    public async Task AddDrnUtils_ShouldRegisterRequiredServices(DrnTestContextUnit context)
    {
        context.ServiceCollection.AddDrnUtils();
        await context.ValidateServicesAsync();

        var appSettings = context.GetRequiredService<IAppSettings>();
        var key = appSettings.NexusAppSettings.GetDefaultKey();
        key.KeyMaterial.Should().Be("SFnefTwiLUfxc_RCX54vHROJQ50TDvqDdHImA2rvrso");
    }
}