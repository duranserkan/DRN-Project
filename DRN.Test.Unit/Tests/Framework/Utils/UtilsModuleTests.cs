using DRN.Framework.Testing.Contexts;
using DRN.Framework.Testing.DataAttributes;
using DRN.Framework.Utils;
using DRN.Framework.Utils.Settings;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

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
        var key = appSettings.Nexus.GetDefaultMacKey();
        key.Key.Should().Be("wN2dC5sO7vVkXpQnYqRtJbZaUxLmKoMhH8GfP4yEI0k=");
    }
}