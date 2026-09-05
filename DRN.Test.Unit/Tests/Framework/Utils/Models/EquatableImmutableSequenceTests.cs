using System.Collections.Immutable;
using DRN.Framework.Utils.Models;

namespace DRN.Test.Unit.Tests.Framework.Utils.Models;

public class EquatableImmutableSequenceTests
{
    [Fact]
    public void EquatableImmutableSequence_CollectionExpression_And_Builder_Should_Initialize_Correctly()
    {
        EquatableImmutableSequence<int> seq = [10, 20, 30];

        seq.Count.Should().Be(3);
        seq.Items.IsDefault.Should().BeFalse();
        seq.Items.Should().Equal(10, 20, 30);

        ReadOnlySpan<string> span = ["x", "y"];
        var built = EquatableSequenceBuilder.CreateImmutable(span);
        built.Count.Should().Be(2);
        built.Items.Should().Equal("x", "y");
    }

    [Fact]
    public void EquatableImmutableSequence_ImplicitOperator_And_Default_Should_Initialize_Correctly()
    {
        var immArray = ImmutableArray.Create(1, 2, 3);
        EquatableImmutableSequence<int> seq = immArray;
        seq.Count.Should().Be(3);
        seq.Items.Should().Equal(immArray);

        EquatableImmutableSequence<int> defaultSeq = default;
        defaultSeq.Count.Should().Be(0);
        defaultSeq.Items.IsDefault.Should().BeTrue();

        EquatableImmutableSequence<int> defaultStructSeq = new(default(ImmutableArray<int>));
        defaultStructSeq.Count.Should().Be(0);
        defaultStructSeq.Items.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void EquatableImmutableSequence_Equality_Should_Handle_Identical_And_Distinct_Sequences()
    {
        EquatableImmutableSequence<int> seq1 = [1, 2, 3];
        EquatableImmutableSequence<int> seq2 = [1, 2, 3];
        EquatableImmutableSequence<int> seqDifferentValues = [1, 2, 4];
        EquatableImmutableSequence<int> seqDifferentLength = [1, 2];
        EquatableImmutableSequence<int> seqDifferentOrder = [3, 2, 1];

        (seq1 == seq2).Should().BeTrue();
        (seq1 != seq2).Should().BeFalse();
        seq1.Equals(seq2).Should().BeTrue();
        seq1.Equals((object)seq2).Should().BeTrue();

        (seq1 == seqDifferentValues).Should().BeFalse();
        (seq1 != seqDifferentValues).Should().BeTrue();
        seq1.Equals(seqDifferentValues).Should().BeFalse();

        (seq1 == seqDifferentLength).Should().BeFalse();
        seq1.Equals(seqDifferentLength).Should().BeFalse();

        (seq1 == seqDifferentOrder).Should().BeFalse();
        seq1.Equals(seqDifferentOrder).Should().BeFalse();

        seq1.Equals((object?)null).Should().BeFalse();
        seq1.Equals("not a sequence").Should().BeFalse();
    }

    [Fact]
    public void EquatableImmutableSequence_Equality_Should_Treat_Default_And_Empty_As_Equal()
    {
        EquatableImmutableSequence<int> defaultSeq = default;
        EquatableImmutableSequence<int> defaultArrSeq = new(default(ImmutableArray<int>));
        EquatableImmutableSequence<int> emptySeq = [];
        EquatableImmutableSequence<int> emptyArraySeq = new(ImmutableArray<int>.Empty);

        (defaultSeq == defaultArrSeq).Should().BeTrue();
        (defaultSeq == emptySeq).Should().BeTrue();
        (defaultSeq == emptyArraySeq).Should().BeTrue();
        (emptySeq == emptyArraySeq).Should().BeTrue();

        defaultSeq.Equals(emptySeq).Should().BeTrue();
        emptySeq.Equals(defaultSeq).Should().BeTrue();
        defaultSeq.Equals((object)emptySeq).Should().BeTrue();

        (defaultSeq != emptySeq).Should().BeFalse();
    }

    [Fact]
    public void EquatableImmutableSequence_UnderlyingArray_Equals_Should_ShortCircuit_True()
    {
        var immArray = ImmutableArray.Create(1, 2, 3);
        EquatableImmutableSequence<int> seq1 = new(immArray);
        EquatableImmutableSequence<int> seq2 = new(immArray);

        (seq1 == seq2).Should().BeTrue();
        seq1.Equals(seq2).Should().BeTrue();
    }

    [Fact]
    public void EquatableImmutableSequence_GetHashCode_Should_Be_Consistent_With_Equality()
    {
        EquatableImmutableSequence<int> seq1 = [1, 2, 3];
        EquatableImmutableSequence<int> seq2 = [1, 2, 3];
        EquatableImmutableSequence<int> defaultSeq = default;
        EquatableImmutableSequence<int> emptySeq = [];

        seq1.GetHashCode().Should().Be(seq2.GetHashCode());
        defaultSeq.GetHashCode().Should().Be(0);
        emptySeq.GetHashCode().Should().Be(0);

        var set = new HashSet<EquatableImmutableSequence<int>> { defaultSeq };
        set.Contains(emptySeq).Should().BeTrue();
        set.Add(seq1).Should().BeTrue();
        set.Contains(seq2).Should().BeTrue();
    }

    [Fact]
    public void EquatableImmutableSequence_Indexer_Should_Return_Element_Or_Throw_On_Invalid_Index()
    {
        EquatableImmutableSequence<string> seq = ["first", "second", "third"];

        seq[0].Should().Be("first");
        seq[1].Should().Be("second");
        seq[2].Should().Be("third");

        var actNegative = () => _ = seq[-1];
        actNegative.Should().Throw<IndexOutOfRangeException>();

        var actOutOfBounds = () => _ = seq[3];
        actOutOfBounds.Should().Throw<IndexOutOfRangeException>();

        EquatableImmutableSequence<string> defaultSeq = default;
        var actDefault = () => _ = defaultSeq[0];
        actDefault.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void EquatableImmutableSequence_Enumeration_Should_Work_Across_All_Interfaces()
    {
        EquatableImmutableSequence<int> seq = [1, 2, 3];

        var collected = new List<int>();
        foreach (var item in seq)
        {
            collected.Add(item);
        }
        collected.Should().Equal(1, 2, 3);

        IEnumerable<int> genericEnumerable = seq;
        genericEnumerable.ToList().Should().Equal(1, 2, 3);

        IEnumerable nonGenericEnumerable = seq;
        var nonGenericList = new List<object?>();
        foreach (var item in nonGenericEnumerable)
        {
            nonGenericList.Add(item);
        }
        nonGenericList.Should().Equal(1, 2, 3);

        EquatableImmutableSequence<int> defaultSeq = default;
        var defaultCollected = new List<int>();
        foreach (var item in defaultSeq)
        {
            defaultCollected.Add(item);
        }
        defaultCollected.Should().BeEmpty();

        ((IEnumerable<int>)defaultSeq).ToList().Should().BeEmpty();
    }

    [Fact]
    public void EquatableImmutableSequence_Deconstruction_And_ToString_Should_Work()
    {
        var immArray = ImmutableArray.Create(7, 8, 9);
        EquatableImmutableSequence<int> seq = immArray;

        seq.Deconstruct(out var items);
        items.Should().Equal(immArray);

        seq.ToString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void EquatableSequence_And_EquatableImmutableSequence_Should_Have_Matching_HashCode()
    {
        EquatableSequence<int> mutableSeq = [1, 2, 3, 4, 5];
        EquatableImmutableSequence<int> immutableSeq = [1, 2, 3, 4, 5];

        mutableSeq.GetHashCode().Should().Be(immutableSeq.GetHashCode());
    }
}
