using System.Diagnostics.CodeAnalysis;
using DRN.Framework.Utils.Models;

namespace DRN.Test.Unit.Tests.Framework.Utils.Models;

[SuppressMessage("ReSharper", "SuspiciousTypeConversion.Global")]
public class EquatableSequenceTests
{
    [Fact]
    public void EquatableSequence_CollectionExpression_And_Builder_Should_Initialize_Correctly()
    {
        EquatableSequence<int> seq = [10, 20, 30];

        seq.Count.Should().Be(3);
        seq.Items.Should().NotBeNull();
        seq.Items.Should().Equal(10, 20, 30);

        ReadOnlySpan<string> span = ["a", "b"];
        var built = EquatableSequenceBuilder.Create(span);
        built.Count.Should().Be(2);
        built.Items.Should().Equal("a", "b");
    }

    [Fact]
    public void EquatableSequence_ImplicitOperator_And_Default_Should_Initialize_Correctly()
    {
        int[] array = [1, 2, 3];
        EquatableSequence<int> seq = array;
        seq.Count.Should().Be(3);
        seq.Items.Should().BeSameAs(array);

        EquatableSequence<int> defaultSeq = default;
        defaultSeq.Count.Should().Be(0);
        defaultSeq.Items.Should().BeNull();

        EquatableSequence<int> nullSeq = new(null);
        nullSeq.Count.Should().Be(0);
        nullSeq.Items.Should().BeNull();
    }

    [Fact]
    public void EquatableSequence_Equality_Should_Handle_Identical_And_Distinct_Sequences()
    {
        EquatableSequence<int> seq1 = [1, 2, 3];
        EquatableSequence<int> seq2 = [1, 2, 3];
        EquatableSequence<int> seqDifferentValues = [1, 2, 4];
        EquatableSequence<int> seqDifferentLength = [1, 2];
        EquatableSequence<int> seqDifferentOrder = [3, 2, 1];

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
    public void EquatableSequence_Equality_Should_Treat_Default_And_Empty_As_Equal()
    {
        EquatableSequence<int> defaultSeq = default;
        EquatableSequence<int> nullSeq = new(null);
        EquatableSequence<int> emptySeq = [];
        EquatableSequence<int> emptyArraySeq = new(Array.Empty<int>());

        (defaultSeq == nullSeq).Should().BeTrue();
        (defaultSeq == emptySeq).Should().BeTrue();
        (defaultSeq == emptyArraySeq).Should().BeTrue();
        (emptySeq == emptyArraySeq).Should().BeTrue();

        defaultSeq.Equals(emptySeq).Should().BeTrue();
        emptySeq.Equals(defaultSeq).Should().BeTrue();
        defaultSeq.Equals((object)emptySeq).Should().BeTrue();

        (defaultSeq != emptySeq).Should().BeFalse();
    }

    [Fact]
    public void EquatableSequence_ReferenceEquals_Should_ShortCircuit_True()
    {
        int[] sharedArray = [1, 2, 3];
        EquatableSequence<int> seq1 = new(sharedArray);
        EquatableSequence<int> seq2 = new(sharedArray);

        (seq1 == seq2).Should().BeTrue();
        seq1.Equals(seq2).Should().BeTrue();
    }

    [Fact]
    public void EquatableSequence_GetHashCode_Should_Be_Consistent_With_Equality()
    {
        EquatableSequence<int> seq1 = [1, 2, 3];
        EquatableSequence<int> seq2 = [1, 2, 3];
        EquatableSequence<int> defaultSeq = default;
        EquatableSequence<int> emptySeq = [];

        seq1.GetHashCode().Should().Be(seq2.GetHashCode());
        defaultSeq.GetHashCode().Should().Be(0);
        emptySeq.GetHashCode().Should().Be(0);

        var set = new HashSet<EquatableSequence<int>> { defaultSeq };
        set.Contains(emptySeq).Should().BeTrue();
        set.Add(seq1).Should().BeTrue();
        set.Contains(seq2).Should().BeTrue();
    }

    [Fact]
    public void EquatableSequence_Indexer_Should_Return_Element_Or_Throw_On_Invalid_Index()
    {
        EquatableSequence<string> seq = ["first", "second", "third"];

        seq[0].Should().Be("first");
        seq[1].Should().Be("second");
        seq[2].Should().Be("third");

        var actNegative = () => _ = seq[-1];
        actNegative.Should().Throw<IndexOutOfRangeException>();

        var actOutOfBounds = () => _ = seq[3];
        actOutOfBounds.Should().Throw<IndexOutOfRangeException>();

        EquatableSequence<string> defaultSeq = default;
        var actDefault = () => _ = defaultSeq[0];
        actDefault.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void EquatableSequence_Enumeration_Should_Work_Across_All_Interfaces()
    {
        EquatableSequence<int> seq = [1, 2, 3];

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

        EquatableSequence<int> defaultSeq = default;
        var defaultCollected = new List<int>();
        foreach (var item in defaultSeq)
            defaultCollected.Add(item);

        defaultCollected.Should().BeEmpty();

        defaultSeq.ToList().Should().BeEmpty();
    }

    [Fact]
    public void EquatableSequence_Deconstruction_And_ToString_Should_Work()
    {
        int[] originalArray = [7, 8, 9];
        EquatableSequence<int> seq = originalArray;

        seq.Deconstruct(out var items);
        items.Should().BeSameAs(originalArray);

        seq.ToString().Should().NotBeNullOrWhiteSpace();
    }
}
