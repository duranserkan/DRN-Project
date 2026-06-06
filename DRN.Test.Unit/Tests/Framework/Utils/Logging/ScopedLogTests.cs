using DRN.Framework.Utils.Logging;

namespace DRN.Test.Unit.Tests.Framework.Utils.Logging;

public class ScopedLogTests
{
    [Fact]
    public void CopyFrom_Should_Merge_Source_Entries_And_Preserve_Destination_State()
    {
        var destination = CreateLog();
        destination.Add("DestinationKey", "destination");
        destination.AddWarning("destination-warning");

        var source = CreateLog();
        source.Add("SourceKey", "source");
        source.AddException(new InvalidOperationException("source-exception"));

        destination.CopyFrom(source);

        var logs = destination.GetLogs();
        logs.Should().ContainKey("DestinationKey");
        logs.Should().ContainKey("SourceKey");
        destination.HasWarning.Should().BeTrue();
        destination.HasException.Should().BeTrue();
    }

    [Fact]
    public void CopyFrom_Should_Clone_Mutable_List_Values()
    {
        var source = CreateLog();
        source.AddToList("Items", "one");

        var destination = CreateLog();
        destination.CopyFrom(source);

        source.AddToList("Items", "two");

        destination.GetLogs()["Items"].Should().BeAssignableTo<List<object>>()
            .Subject.Should().Equal("one");
    }

    private static ScopedLog CreateLog() => new(AppSettings.Development());
}
