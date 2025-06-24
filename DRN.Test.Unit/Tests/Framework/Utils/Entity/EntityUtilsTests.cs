using DRN.Framework.Utils.Entity;

namespace DRN.Test.Unit.Tests.Framework.Utils.Entity;

public class EntityUtilsTests
{
    [Theory]
    [DataInlineUnit]
    public void SourceKnownIDs_Should_Be_Generate_Id(UnitTestContext context)
    {
        var entityUtils = context.GetRequiredService<IEntityUtils>();

        entityUtils.IdUtils.Should().NotBeNull();
        entityUtils.EntityIdUtils.Should().NotBeNull();
        entityUtils.PaginationUtils.Should().NotBeNull();
        entityUtils.DateTimeUtils.Should().NotBeNull();
    }
}