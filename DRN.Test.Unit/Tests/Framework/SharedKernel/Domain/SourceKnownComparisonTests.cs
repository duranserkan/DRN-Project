using DRN.Framework.SharedKernel.Domain;

namespace DRN.Test.Unit.Tests.Framework.SharedKernel.Domain;

public class SourceKnownComparisonTests
{
    [Theory]
    [DataInlineUnit(5, 7, true)]
    [DataInlineUnit(5, 8, false)]
    [DataInlineUnit(6, 7, false)]
    [DataInlineUnit(6, 8, false)]
    public void HasSameEntityTypeId_Should_Compare_Partition_And_Type_For_Both_Identity_Overloads(
        byte otherApp, byte otherType, bool expected)
    {
        var first = new SourceKnownEntityId(new SourceKnownId(10, default, 0, 5, 0), Guid.NewGuid(), 7, true, false);
        var other = new SourceKnownEntityId(new SourceKnownId(20, default, 0, otherApp, 0), Guid.NewGuid(), otherType, true, true);

        first.HasSameEntityTypeId(other).Should().Be(expected);
        other.HasSameEntityTypeId(first).Should().Be(expected);
        first.HasSameEntityTypeId(new EntityTypeId(otherType, otherApp)).Should().Be(expected);
        other.HasSameEntityTypeId(new EntityTypeId(7, 5)).Should().Be(expected);
    }

    [Theory]
    [DataInlineUnit(0, 7, 20L, 0, 8, 10L)]
    [DataInlineUnit(0, 8, 20L, 1, 7, 10L)]
    [DataInlineUnit(0, 7, 20L, 1, 7, 10L)]
    [DataInlineUnit(0, 7, 10L, 0, 7, 20L)]
    public void Assigned_Identities_Should_Order_By_Partition_Type_Then_Id(
        byte firstApp, byte firstType, long firstId, byte secondApp, byte secondType, long secondId)
    {
        var firstSource = new SourceKnownEntityId(new SourceKnownId(firstId, default, 0, firstApp, 0), Guid.NewGuid(), firstType, true, false);
        var secondSource = new SourceKnownEntityId(new SourceKnownId(secondId, default, 0, secondApp, 0), Guid.NewGuid(), secondType, true, false);

        firstSource.CompareTo(secondSource).Should().BeLessThan(0);
        secondSource.CompareTo(firstSource).Should().BeGreaterThan(0);
        (firstSource < secondSource).Should().BeTrue();
        (secondSource > firstSource).Should().BeTrue();
        (firstSource > secondSource).Should().BeFalse();
        var ids = new SortedSet<SourceKnownEntityId> { secondSource, firstSource };
        ids.Should().Equal(firstSource, secondSource);
        ids.Contains(firstSource).Should().BeTrue();
        ids.Add(firstSource).Should().BeFalse();

        SourceKnownEntity first = new CustomTestEntity { Id = firstId, EntityIdSource = firstSource };
        SourceKnownEntity second = new CustomTestEntity { Id = secondId, EntityIdSource = secondSource };

        first.CompareTo(second).Should().BeLessThan(0);
        second.CompareTo(first).Should().BeGreaterThan(0);
        (first < second).Should().BeTrue();
        (second > first).Should().BeTrue();
        (first > second).Should().BeFalse();
        var entities = new SortedSet<SourceKnownEntity> { second, first };
        entities.Should().Equal(first, second);
        entities.Contains(first).Should().BeTrue();
        entities.Add(first).Should().BeFalse();
    }

    [Fact]
    public void Comparison_Should_Preserve_Identity_Null_And_Transient_Precedence()
    {
        var source = new SourceKnownEntityId(new SourceKnownId(10, default, 0, 42, 0), Guid.NewGuid(), 7, true, false);
        SourceKnownEntity entity = new CustomTestEntity { Id = 10, EntityIdSource = source };
        SourceKnownEntity duplicate = new CustomTestEntity { Id = 10, EntityIdSource = source };
        SourceKnownEntity transient = new CustomTestEntity();

        source.CompareTo(source).Should().Be(0);
        var secureVariant = source with { EntityId = Guid.NewGuid(), Secure = true };
        source.CompareTo(secureVariant).Should().Be(0);
        secureVariant.CompareTo(source).Should().Be(0);
        entity.CompareTo(duplicate).Should().Be(0);
        entity.CompareTo(null).Should().Be(1);
        entity.CompareTo(transient).Should().Be(1);
        transient.CompareTo(entity).Should().Be(-1);
        transient.CompareTo(transient).Should().Be(0);
        transient.CompareTo(null).Should().Be(1);
        ((SourceKnownEntity?)null < entity).Should().BeTrue();
    }
}
