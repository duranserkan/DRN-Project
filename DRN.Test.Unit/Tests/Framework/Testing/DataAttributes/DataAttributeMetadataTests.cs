using Xunit.Sdk;
using Xunit.v3;

namespace DRN.Test.Unit.Tests.Framework.Testing.DataAttributes;

public class DataAttributeMetadataTests
{
    private const int DataValue = 42;
    private const int SourceDataValue = 43;
    private const int InheritedDataValue = 44;
    private const int SourceOwnsEmptySkipGroupDataValue = 45;
    private const int OuterOwnsSkipGroupDataValue = 46;
    private const int SecondSelfDataValue = 47;
    private const string OuterLabel = "outer-label";
    private const string OuterSkip = "outer-skip";
    private const string OuterDisplayName = "outer-display-name";
    private const string OuterTrait = "outer-trait";
    private const string OuterTraitValue = "outer-value";
    private const string ProviderLabel = "provider-label";
    private const string ProviderSkip = "provider-skip";
    private const string ProviderDisplayName = "provider-display-name";
    private const string ProviderTrait = "provider-trait";
    private const string ProviderTraitValue = "provider-value";
    private const string SourceTrait = "source-trait";
    private const string SourceTraitValue = "source-value";
    private const string SharedTrait = "shared-trait";
    private const string OuterSharedTraitValue = "outer-shared-value";
    private const string ProviderSharedTraitValue = "provider-shared-value";
    private const string SourceSharedTraitValue = "source-shared-value";
    private const int OuterTimeout = 1_234;
    private const int ProviderTimeout = 432;

    [Fact]
    public async Task Wrappers_Should_Preserve_Outer_Attribute_Metadata()
    {
        DataAttribute[] attributes =
        [
            ConfigureOuterMetadata(new DataInlineAttribute(DataValue)),
            ConfigureOuterMetadata(new DataInlineUnitAttribute(DataValue)),
            ConfigureOuterMetadata(new DataMemberAttribute(nameof(UnadornedRows))
            {
                MemberType = typeof(DataAttributeMetadataTests)
            }),
            ConfigureOuterMetadata(new DataMemberUnitAttribute(nameof(UnadornedRows))
            {
                MemberType = typeof(DataAttributeMetadataTests)
            })
        ];

        var testMethod = GetMetadataTarget();
        await using var disposalTracker = new DisposalTracker();

        foreach (var attribute in attributes)
        {
            var rows = await attribute.GetData(testMethod, disposalTracker);

            rows.Should().ContainSingle();
            AssertData(rows.Single(), DataValue);
            AssertOuterMetadata(rows.Single());
        }
    }

    [Fact]
    public async Task Self_Wrappers_Should_Preserve_Outer_Attribute_Metadata_For_Every_Row()
    {
        DataAttribute[] attributes =
        [
            ConfigureOuterMetadata(new MetadataDataSelfAttribute()),
            ConfigureOuterMetadata(new MetadataDataSelfUnitAttribute())
        ];

        var testMethod = GetMetadataTarget();
        await using var disposalTracker = new DisposalTracker();

        foreach (var attribute in attributes)
        {
            var rows = await attribute.GetData(testMethod, disposalTracker);

            rows.Should().HaveCount(2);
            AssertData(rows.Single(row => Equals(row.GetData().Single(), DataValue)), DataValue);
            AssertData(rows.Single(row => Equals(row.GetData().Single(), SecondSelfDataValue)), SecondSelfDataValue);
            foreach (var row in rows)
                AssertOuterMetadata(row);
        }
    }

    [Fact]
    public async Task DataInlineUnit_Should_Preserve_Outer_Attribute_Metadata_With_Unit_Context()
    {
        var attribute = ConfigureOuterMetadata(new DataInlineUnitAttribute(DataValue));
        var testMethod = GetMetadataTarget(nameof(MetadataUnitContextTarget));
        await using var disposalTracker = new DisposalTracker();

        var row = (await attribute.GetData(testMethod, disposalTracker)).Single();
        var rowData = row.GetData();
        using var context = (DrnTestContextUnit)rowData[0]!;

        rowData.Should().HaveCount(2);
        rowData[1].Should().Be(DataValue);
        context.MethodContext.TestMethod.Should().BeSameAs(testMethod);
        AssertOuterMetadata(row);
    }

    [Fact]
    public async Task Member_Wrappers_Should_Merge_Source_Row_And_Outer_Attribute_Metadata()
    {
        DataAttribute[] attributes =
        [
            ConfigureOuterMetadata(new DataMemberAttribute(nameof(MetadataRows))
            {
                MemberType = typeof(DataAttributeMetadataTests)
            }),
            ConfigureOuterMetadata(new DataMemberUnitAttribute(nameof(MetadataRows))
            {
                MemberType = typeof(DataAttributeMetadataTests)
            })
        ];

        var testMethod = GetMetadataTarget();
        await using var disposalTracker = new DisposalTracker();

        foreach (var attribute in attributes)
        {
            var rows = await attribute.GetData(testMethod, disposalTracker);
            var sourceRow = rows.Single(row => Equals(row.GetData().Single(), SourceDataValue));
            var inheritedRow = rows.Single(row => Equals(row.GetData().Single(), InheritedDataValue));
            var sourceOwnsEmptySkipGroupRow = rows.Single(row =>
                Equals(row.GetData().Single(), SourceOwnsEmptySkipGroupDataValue));
            var outerOwnsSkipGroupRow = rows.Single(row =>
                Equals(row.GetData().Single(), OuterOwnsSkipGroupDataValue));

            AssertSourceMetadata(sourceRow);
            AssertData(inheritedRow, InheritedDataValue);
            AssertOuterMetadata(inheritedRow);
            AssertSourceOwnsEmptySkipGroup(sourceOwnsEmptySkipGroupRow);
            AssertData(outerOwnsSkipGroupRow, OuterOwnsSkipGroupDataValue);
            AssertOuterMetadata(outerOwnsSkipGroupRow);
        }
    }

    [Fact]
    public void Attribute_Metadata_Should_Override_Generated_Metadata_And_Merge_Traits_Case_Insensitively()
    {
        var generatedRow = CreateProviderMetadataRow();
        var attribute = ConfigureOuterMetadata(new DataInlineUnitAttribute());

        var row = TheoryDataRowMetadata.ApplyAttributeToGeneratedRow(generatedRow, attribute);

        AssertData(row, DataValue);
        AssertOuterMetadata(row);
        AssertTrait(row, ProviderTrait, ProviderTraitValue);
        AssertTrait(row, SharedTrait, ProviderSharedTraitValue);
        row.Traits!.Keys.Count(key =>
            string.Equals(key, SharedTrait, StringComparison.OrdinalIgnoreCase)).Should().Be(1);
    }

    [Fact]
    public void Unconfigured_Attribute_Should_Preserve_Generated_Metadata()
    {
        var generatedRow = CreateProviderMetadataRow();

        var row = TheoryDataRowMetadata.ApplyAttributeToGeneratedRow(
            generatedRow,
            new DataInlineUnitAttribute());

        AssertData(row, DataValue);
        row.DisableParallelization.Should().Be(false);
        row.Explicit.Should().Be(false);
        row.Label.Should().Be(ProviderLabel);
        row.Skip.Should().Be(ProviderSkip);
        row.SkipType.Should().Be(typeof(ProviderSkipConditions));
        row.SkipUnless.Should().Be(nameof(ProviderSkipConditions.IsMet));
        row.SkipWhen.Should().BeNull();
        row.TestDisplayName.Should().Be(ProviderDisplayName);
        row.Timeout.Should().Be(ProviderTimeout);
        AssertTrait(row, ProviderTrait, ProviderTraitValue);
        AssertTrait(row, SharedTrait, ProviderSharedTraitValue);
    }

    [Fact]
    public void Unset_Metadata_Should_Remain_Null_For_Theory_Inheritance()
    {
        var row = TheoryDataRowMetadata.ApplyAttributeToGeneratedRow(
            new TheoryDataRow(DataValue),
            new DataInlineUnitAttribute());

        AssertData(row, DataValue);
        row.DisableParallelization.Should().Be(false);
        row.Explicit.Should().BeNull();
        row.Label.Should().BeNull();
        row.Skip.Should().BeNull();
        row.SkipType.Should().BeNull();
        row.SkipUnless.Should().BeNull();
        row.SkipWhen.Should().BeNull();
        row.TestDisplayName.Should().BeNull();
        row.Timeout.Should().BeNull();
        row.Traits.Should().NotBeNull();
        row.Traits!.Should().BeEmpty();
    }

    public static IEnumerable<object[]> UnadornedRows =>
    [
        [DataValue]
    ];

    public static IEnumerable<ITheoryDataRow> MetadataRows =>
    [
        new TheoryDataRow(SourceDataValue)
        {
            DisableParallelization = false,
            Explicit = false,
            Label = string.Empty,
            Skip = "source-skip",
            SkipType = typeof(SourceSkipConditions),
            SkipUnless = nameof(SourceSkipConditions.IsMet),
            TestDisplayName = string.Empty,
            Timeout = 0,
            Traits = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                [SourceTrait] = [SourceTraitValue],
                ["SHARED-TRAIT"] = [SourceSharedTraitValue]
            }
        },
        new TheoryDataRow(InheritedDataValue),
        new TheoryDataRow(SourceOwnsEmptySkipGroupDataValue)
        {
            Skip = "source-skip-without-condition"
        },
        new TheoryDataRow(OuterOwnsSkipGroupDataValue)
        {
            SkipType = typeof(OrphanSkipConditions),
            SkipUnless = nameof(OrphanSkipConditions.IsMet)
        }
    ];

    private static T ConfigureOuterMetadata<T>(T attribute)
        where T : DataAttribute
    {
        attribute.DisableParallelization = true;
        attribute.Explicit = true;
        attribute.Label = OuterLabel;
        attribute.Skip = OuterSkip;
        attribute.SkipType = typeof(OuterSkipConditions);
        attribute.SkipWhen = nameof(OuterSkipConditions.IsMet);
        attribute.TestDisplayName = OuterDisplayName;
        attribute.Timeout = OuterTimeout;
        attribute.Traits =
        [
            OuterTrait, OuterTraitValue,
            SharedTrait, OuterSharedTraitValue
        ];

        return attribute;
    }

    private static TheoryDataRow CreateProviderMetadataRow() =>
        new(DataValue)
        {
            DisableParallelization = false,
            Explicit = false,
            Label = ProviderLabel,
            Skip = ProviderSkip,
            SkipType = typeof(ProviderSkipConditions),
            SkipUnless = nameof(ProviderSkipConditions.IsMet),
            TestDisplayName = ProviderDisplayName,
            Timeout = ProviderTimeout,
            Traits = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                [ProviderTrait] = [ProviderTraitValue],
                ["SHARED-TRAIT"] = [ProviderSharedTraitValue]
            }
        };

    private static MethodInfo GetMetadataTarget(string methodName = nameof(MetadataTarget)) =>
        typeof(DataAttributeMetadataTests).GetMethod(methodName, BindingFlag.StaticNonPublic)!;

    private static void MetadataTarget(int value)
    {
    }

    private static void MetadataUnitContextTarget(DrnTestContextUnit context, int value)
    {
    }

    private static void AssertData(ITheoryDataRow row, int expectedData)
    {
        row.GetData().Should().ContainSingle().Which.Should().Be(expectedData);
    }

    private static void AssertOuterMetadata(ITheoryDataRow row)
    {
        row.DisableParallelization.Should().Be(true);
        row.Explicit.Should().Be(true);
        row.Label.Should().Be(OuterLabel);
        row.Skip.Should().Be(OuterSkip);
        row.SkipType.Should().Be(typeof(OuterSkipConditions));
        row.SkipUnless.Should().BeNull();
        row.SkipWhen.Should().Be(nameof(OuterSkipConditions.IsMet));
        row.TestDisplayName.Should().Be(OuterDisplayName);
        row.Timeout.Should().Be(OuterTimeout);
        AssertTrait(row, OuterTrait, OuterTraitValue);
        AssertTrait(row, SharedTrait, OuterSharedTraitValue);
    }

    private static void AssertSourceMetadata(ITheoryDataRow row)
    {
        row.DisableParallelization.Should().Be(false);
        row.Explicit.Should().Be(false);
        row.Label.Should().BeEmpty();
        row.Skip.Should().Be("source-skip");
        row.SkipType.Should().Be(typeof(SourceSkipConditions));
        row.SkipUnless.Should().Be(nameof(SourceSkipConditions.IsMet));
        row.SkipWhen.Should().BeNull();
        row.TestDisplayName.Should().BeEmpty();
        row.Timeout.Should().Be(0);
        AssertTrait(row, SourceTrait, SourceTraitValue);
        AssertTrait(row, SharedTrait, SourceSharedTraitValue);
        AssertTrait(row, SharedTrait, OuterSharedTraitValue);
        AssertTrait(row, OuterTrait, OuterTraitValue);
    }

    private static void AssertSourceOwnsEmptySkipGroup(ITheoryDataRow row)
    {
        AssertData(row, SourceOwnsEmptySkipGroupDataValue);
        row.DisableParallelization.Should().Be(true);
        row.Explicit.Should().Be(true);
        row.Label.Should().Be(OuterLabel);
        row.Skip.Should().Be("source-skip-without-condition");
        row.SkipType.Should().BeNull();
        row.SkipUnless.Should().BeNull();
        row.SkipWhen.Should().BeNull();
        row.TestDisplayName.Should().Be(OuterDisplayName);
        row.Timeout.Should().Be(OuterTimeout);
        AssertTrait(row, OuterTrait, OuterTraitValue);
        AssertTrait(row, SharedTrait, OuterSharedTraitValue);
    }

    private static void AssertTrait(ITheoryDataRow row, string name, string value)
    {
        row.Traits.Should().NotBeNull();
        row.Traits!.ContainsKey(name).Should().BeTrue();
        row.Traits[name].Should().Contain(value);
    }

    public static class OuterSkipConditions
    {
        public static bool IsMet => false;
    }

    public static class SourceSkipConditions
    {
        public static bool IsMet => true;
    }

    public static class ProviderSkipConditions
    {
        public static bool IsMet => true;
    }

    public static class OrphanSkipConditions
    {
        public static bool IsMet => true;
    }
}

public sealed class MetadataDataSelfAttribute : DataSelfAttribute
{
    public MetadataDataSelfAttribute()
    {
        AddRow(42);
        AddRow(47);
    }
}

public sealed class MetadataDataSelfUnitAttribute : DataSelfUnitAttribute
{
    public MetadataDataSelfUnitAttribute()
    {
        AddRow(42);
        AddRow(47);
    }
}
