using DRN.Framework.Utils.Entity;

namespace DRN.Test.Unit.Tests.Framework.Utils.Entity;

public class EntityUtilsTests
{
    [Theory]
    [DataInlineUnit]
    public void SourceKnownIDs_Should_Be_Generate_Id(TestContextUnit context)
    {
        var entityUtils = context.GetRequiredService<IEntityUtils>();

        entityUtils.Id.Should().NotBeNull();
        entityUtils.EntityId.Should().NotBeNull();
        entityUtils.Pagination.Should().NotBeNull();
        entityUtils.DateTime.Should().NotBeNull();
    }
}