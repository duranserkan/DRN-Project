using DRN.Framework.Utils;

namespace DRN.Test.Unit.Tests.Framework.Utils;

public class UtilsModuleTests
{
    [Theory]
    [DataInlineUnit]
    public void AddDrnUtils_ShouldRegisterRequiredServices(UnitTestContext context)
    {
        context.ServiceCollection.AddDrnUtils();
        context.ValidateServices();

        var appSettings = context.GetRequiredService<IAppSettings>();
        var key = appSettings.NexusAppSettings.GetDefaultMacKey();
        key.Key.Should().Be("wN2dC5sO7vVkXpQnYqRtJbZaUxLmKoMhH8GfP4yEI0k=");
    }
}