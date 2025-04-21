using DRN.Framework.Testing.Contexts;
using DRN.Framework.Testing.DataAttributes;
using DRN.Framework.Utils;
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
    }
}