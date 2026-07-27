using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Entity;
using DRN.Framework.Utils.Ids;
using DRN.Framework.Utils.Time;

namespace DRN.Test.Unit.Tests.Framework.Utils.Entity;

public class EntityDateTimeUtilsTests
{
    private const long PayloadMask = 0x7FFF_FFFF;
    private static readonly DateTimeOffset Boundary = EpochTimeUtils.DefaultEpoch.AddDays(1);
    private static readonly long TickMin = ConvertToTickMin(Boundary);
    private static readonly long TickMax = TickMin | PayloadMask;
    private static readonly long PreviousTickId = ConvertToTickMin(Boundary.AddMilliseconds(-250)) | PayloadMask;
    private static readonly long NextTickId = ConvertToTickMin(Boundary.AddMilliseconds(250));
    private static readonly long[] BoundaryTickIds = [TickMin, TickMin | 1, TickMin | 0x1234_5678, TickMax];
    private static readonly DateTimeOffset RangeEnd = Boundary.AddMilliseconds(500);
    private static readonly long RangeMiddleId = ConvertToTickMin(Boundary.AddMilliseconds(250)) | 0x1234_5678;
    private static readonly long RangeEndMin = ConvertToTickMin(RangeEnd);
    private static readonly long RangeEndMax = RangeEndMin | PayloadMask;
    private static readonly long RangeAfterId = ConvertToTickMin(RangeEnd.AddMilliseconds(250));
    private static readonly long[] RangeQueryIds =
        [PreviousTickId, TickMin, TickMax, RangeMiddleId, RangeEndMin, RangeEndMax, RangeAfterId];
    private readonly EntityDateTimeUtils _dateTimeUtils = new();

    [Fact]
    public void CreatedAfter_Should_Respect_Whole_Boundary_Tick()
    {
        var inclusiveIds = _dateTimeUtils.CreatedAfter(CreateQuery(), Boundary).Select(entity => entity.Id);
        var exclusiveIds = _dateTimeUtils.CreatedAfter(CreateQuery(), Boundary, inclusive: false).Select(entity => entity.Id);

        inclusiveIds.Should().Equal(BoundaryTickIds.Append(NextTickId));
        exclusiveIds.Should().Equal(NextTickId);
    }

    [Fact]
    public void CreatedBefore_Should_Respect_Whole_Boundary_Tick()
    {
        var inclusiveIds = _dateTimeUtils.CreatedBefore(CreateQuery(), Boundary).Select(entity => entity.Id);
        var exclusiveIds = _dateTimeUtils.CreatedBefore(CreateQuery(), Boundary, inclusive: false).Select(entity => entity.Id);

        inclusiveIds.Should().Equal(BoundaryTickIds.Prepend(PreviousTickId));
        exclusiveIds.Should().Equal(PreviousTickId);
    }

    [Fact]
    public void CreatedBetween_Should_Respect_Equal_Tick_Bounds()
    {
        var inclusiveIds = _dateTimeUtils.CreatedBetween(CreateQuery(), Boundary, Boundary).Select(entity => entity.Id);
        var exclusiveIds = _dateTimeUtils.CreatedBetween(CreateQuery(), Boundary, Boundary, inclusive: false).Select(entity => entity.Id);

        inclusiveIds.Should().Equal(BoundaryTickIds);
        exclusiveIds.Should().BeEmpty();
    }

    [Fact]
    public void CreatedOutside_Should_Respect_Equal_Tick_Bounds()
    {
        var inclusiveIds = _dateTimeUtils.CreatedOutside(CreateQuery(), Boundary, Boundary).Select(entity => entity.Id);
        var exclusiveIds = _dateTimeUtils.CreatedOutside(CreateQuery(), Boundary, Boundary, inclusive: false).Select(entity => entity.Id);

        inclusiveIds.Should().Equal(BoundaryTickIds.Prepend(PreviousTickId).Append(NextTickId));
        exclusiveIds.Should().Equal(PreviousTickId, NextTickId);
    }

    [Fact]
    public void CreatedBetween_Should_Respect_Distinct_And_Reversed_Tick_Bounds()
    {
        var inclusiveIds = _dateTimeUtils.CreatedBetween(CreateRangeQuery(), Boundary, RangeEnd).Select(entity => entity.Id);
        var exclusiveIds = _dateTimeUtils.CreatedBetween(CreateRangeQuery(), Boundary, RangeEnd, inclusive: false).Select(entity => entity.Id);
        var reversedInclusiveIds = _dateTimeUtils.CreatedBetween(CreateRangeQuery(), RangeEnd, Boundary).Select(entity => entity.Id);
        var reversedExclusiveIds = _dateTimeUtils.CreatedBetween(CreateRangeQuery(), RangeEnd, Boundary, inclusive: false).Select(entity => entity.Id);

        long[] expectedInclusiveIds = [TickMin, TickMax, RangeMiddleId, RangeEndMin, RangeEndMax];

        inclusiveIds.Should().Equal(expectedInclusiveIds);
        exclusiveIds.Should().Equal(RangeMiddleId);
        reversedInclusiveIds.Should().Equal(expectedInclusiveIds);
        reversedExclusiveIds.Should().Equal(RangeMiddleId);
    }

    [Fact]
    public void CreatedOutside_Should_Respect_Distinct_And_Reversed_Tick_Bounds()
    {
        var inclusiveIds = _dateTimeUtils.CreatedOutside(CreateRangeQuery(), Boundary, RangeEnd).Select(entity => entity.Id);
        var exclusiveIds = _dateTimeUtils.CreatedOutside(CreateRangeQuery(), Boundary, RangeEnd, inclusive: false).Select(entity => entity.Id);
        var reversedInclusiveIds = _dateTimeUtils.CreatedOutside(CreateRangeQuery(), RangeEnd, Boundary).Select(entity => entity.Id);
        var reversedExclusiveIds = _dateTimeUtils.CreatedOutside(CreateRangeQuery(), RangeEnd, Boundary, inclusive: false).Select(entity => entity.Id);

        long[] expectedInclusiveIds = [PreviousTickId, TickMin, TickMax, RangeEndMin, RangeEndMax, RangeAfterId];

        inclusiveIds.Should().Equal(expectedInclusiveIds);
        exclusiveIds.Should().Equal(PreviousTickId, RangeAfterId);
        reversedInclusiveIds.Should().Equal(expectedInclusiveIds);
        reversedExclusiveIds.Should().Equal(PreviousTickId, RangeAfterId);
    }

    [Fact]
    public void CreatedAfter_And_Before_Should_Respect_Epoch_Half_Transition()
    {
        var halfBoundary = EpochTimeUtils.ConvertToDateTime(SourceKnownIdUtils.TicksPerHalf, EpochTimeUtils.DefaultEpoch);
        var previousHalfId = ConvertToTickMin(halfBoundary.AddMilliseconds(-250)) | PayloadMask;
        var halfTickMin = ConvertToTickMin(halfBoundary);
        var halfTickMax = halfTickMin | PayloadMask;
        var nextHalfId = ConvertToTickMin(halfBoundary.AddMilliseconds(250));
        var query = CreateQuery(previousHalfId, halfTickMin, halfTickMax, nextHalfId);

        var afterInclusiveIds = _dateTimeUtils.CreatedAfter(query, halfBoundary).Select(entity => entity.Id);
        var afterExclusiveIds = _dateTimeUtils.CreatedAfter(query, halfBoundary, inclusive: false).Select(entity => entity.Id);
        var beforeInclusiveIds = _dateTimeUtils.CreatedBefore(query, halfBoundary).Select(entity => entity.Id);
        var beforeExclusiveIds = _dateTimeUtils.CreatedBefore(query, halfBoundary, inclusive: false).Select(entity => entity.Id);

        afterInclusiveIds.Should().Equal(halfTickMin, halfTickMax, nextHalfId);
        afterExclusiveIds.Should().Equal(nextHalfId);
        beforeInclusiveIds.Should().Equal(previousHalfId, halfTickMin, halfTickMax);
        beforeExclusiveIds.Should().Equal(previousHalfId);
    }

    private static IQueryable<TestEntity> CreateQuery() => BoundaryTickIds
        .Prepend(PreviousTickId)
        .Append(NextTickId)
        .Select(id => new TestEntity(id))
        .AsQueryable();

    private static IQueryable<TestEntity> CreateRangeQuery() => CreateQuery(RangeQueryIds);

    private static IQueryable<TestEntity> CreateQuery(params long[] ids) => ids
        .Select(id => new TestEntity(id))
        .AsQueryable();

    private static long ConvertToTickMin(DateTimeOffset date) =>
        EpochTimeUtils.ConvertToSourceKnownIdTimeStamp(date, EpochTimeUtils.DefaultEpoch);

    private sealed class TestEntity(long id) : SourceKnownEntity(id);
}
