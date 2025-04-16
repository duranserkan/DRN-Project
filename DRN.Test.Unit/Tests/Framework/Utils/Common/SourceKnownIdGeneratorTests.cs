using DRN.Framework.Utils.Ids;
using FluentAssertions;
using Xunit;

namespace DRN.Test.Unit.Tests.Framework.Utils.Common;

public class SourceKnownIdGeneratorTests
{
    [Fact]
    public async Task Generator_Should_Generate_Valid_Id()
    {
        byte appId = 1;
        byte appInstanceId = 1;
        
        var epoch = SourceKnownIdGenerator.Epoch2025;
        var beforeIdGenerated = DateTimeOffset.UtcNow;
        
        await Task.Delay(1000);
        var id = SourceKnownIdGenerator.GenerateId<object>(appId, appInstanceId);
        await Task.Delay(1000);
        
        var afterIdGenerated = DateTimeOffset.UtcNow;
        var idInfo = SourceKnownIdGenerator.ParseId(id);
        
        idInfo.Id.Should().Be(id);
        idInfo.AppId.Should().Be(appId);
        idInfo.AppInstanceId.Should().Be(appInstanceId);

        epoch.Should().BeBefore(beforeIdGenerated);
        idInfo.CreatedAt.Should().BeBefore(afterIdGenerated);
        idInfo.CreatedAt.Should().BeAfter(beforeIdGenerated);
    }
}