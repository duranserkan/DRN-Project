using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.Testing.Extensions;
using DRN.Framework.Utils.Data.Serialization;

namespace DRN.Test.Unit.Tests.Framework.SharedKernel.Pagination;

public class PaginationTests
{
    [Fact]
    public void PageSize_Defaults()
    {
        var pageSize = PageSize.Default;
        pageSize.Size.Should().Be(10);
        pageSize.MaxSize.Should().Be(100);

        pageSize.ValidateObjectSerialization();
    }

    [Theory]
    [DataInlineUnit(99, null, 99, 100)]
    [DataInlineUnit(101, null, 100, 100)]
    [DataInlineUnit(0, null, 1, 100)]
    [DataInlineUnit(0, 0, 1, 100)]
    [DataInlineUnit(50, 30, 30, 30)]
    [DataInlineUnit(150, 1001, 150, 1000)]
    [DataInlineUnit(1500, 1001, 1000, 1000)]
    public void PageSize_Should_Normalize_Size_And_MaxSize_And_Roundtrip(
        int size, int? maxSize, int expectedSize, int expectedMaxSize)
    {
        var pageSize = maxSize.HasValue ? new PageSize(size, maxSize.Value) : new PageSize(size);
        pageSize.Size.Should().Be(expectedSize);
        pageSize.MaxSize.Should().Be(expectedMaxSize);

        pageSize.ValidateObjectSerialization();
    }

    [Theory]
    [DataInlineUnit(150, 150, 150)]
    [DataInlineUnit(1500, 1001, 1000)]
    public void PageSize_MaxSize_Threshold_Override_Should_Not_Survive_Serialization(
        int size, int expectedSize, int expectedDeserializedSize)
    {
        var pageSize = new PageSize(size, 1001, true);
        pageSize.Size.Should().Be(expectedSize);
        pageSize.MaxSize.Should().Be(1001);

        var json = pageSize.Serialize();
        var deserializedObj = json.Deserialize<PageSize>()!;

        deserializedObj.Size.Should().Be(expectedDeserializedSize);
        deserializedObj.MaxSize.Should().Be(PageSize.MaxSizeThreshold); //prevent override maxsize from serializations
    }
}
