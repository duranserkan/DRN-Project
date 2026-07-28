using Xunit.Sdk;

namespace DRN.Test.Integration.Tests.Framework.Testing.DataAttributes;

public class DataAttributeMetadataIntegrationTests
{
    private const int DataValue = 42;
    private const string OuterLabel = "outer-label";
    private const string OuterSkip = "outer-skip";
    private const string OuterDisplayName = "outer-display-name";
    private const string OuterTrait = "outer-trait";
    private const string OuterTraitValue = "outer-value";
    private const int OuterTimeout = 1_234;

    [Fact]
    public async Task DataInline_Should_Preserve_Outer_Attribute_Metadata_With_Integration_Context()
    {
        var attribute = new DataInlineAttribute(DataValue)
        {
            DisableParallelization = true,
            Explicit = true,
            Label = OuterLabel,
            Skip = OuterSkip,
            SkipType = typeof(OuterSkipConditions),
            SkipWhen = nameof(OuterSkipConditions.IsMet),
            TestDisplayName = OuterDisplayName,
            Timeout = OuterTimeout,
            Traits = [OuterTrait, OuterTraitValue]
        };
        var testMethod = typeof(DataAttributeMetadataIntegrationTests).GetMethod(
            nameof(MetadataContextTarget),
            BindingFlags.NonPublic | BindingFlags.Static)!;
        await using var disposalTracker = new DisposalTracker();

        var row = (await attribute.GetData(testMethod, disposalTracker)).Single();
        var rowData = row.GetData();
        using var context = (DrnTestContext)rowData[0]!;

        rowData.Should().HaveCount(2);
        rowData[1].Should().Be(DataValue);
        context.MethodContext.TestMethod.Name.Should().Be(nameof(MetadataContextTarget));
        row.DisableParallelization.Should().Be(true);
        row.Explicit.Should().Be(true);
        row.Label.Should().Be(OuterLabel);
        row.Skip.Should().Be(OuterSkip);
        row.SkipType.Should().Be(typeof(OuterSkipConditions));
        row.SkipUnless.Should().BeNull();
        row.SkipWhen.Should().Be(nameof(OuterSkipConditions.IsMet));
        row.TestDisplayName.Should().Be(OuterDisplayName);
        row.Timeout.Should().Be(OuterTimeout);
        row.Traits.Should().NotBeNull();
        row.Traits![OuterTrait].Should().Contain(OuterTraitValue);
    }

    private static void MetadataContextTarget(DrnTestContext context, int value)
    {
    }

    public static class OuterSkipConditions
    {
        public static bool IsMet => false;
    }
}
