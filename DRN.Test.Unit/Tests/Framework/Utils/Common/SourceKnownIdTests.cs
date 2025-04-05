using DRN.Framework.Testing.Contexts;
using DRN.Framework.Testing.DataAttributes;
using DRN.Framework.Utils.Common;
using DRN.Framework.Utils.Settings;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DRN.Test.Unit.Tests.Framework.Utils.Common;

public class SourceKnownIdTests
{
    [Theory]
    [DataInlineUnit]
    public async Task SourceKnownIDs_Should_Be_Generate_Id(UnitTestContext context)
    {
        var nexusSettings = new NexusAppSettings
        {
            NexusAppId = 5,
            NexusAppInstanceId = 12
        };

        var customSettings = new
        {
            NexusAppSettings = nexusSettings
        };

        context.AddToConfiguration(customSettings);
        var generator = context.GetRequiredService<ISourceKnownIdGenerator>();
        
        var epoch = IdGenerator.Epoch2025;
        var beforeIdGenerated = DateTimeOffset.UtcNow;
        
        await Task.Delay(1000);
        var id =  generator.NextId<SourceKnownIdTests>();
        await Task.Delay(1000);
        
        var afterIdGenerated = DateTimeOffset.UtcNow;
        
        id.Should().BeNegative();

        var idInfo = generator.Parse(id);
        idInfo.AppId.Should().Be(nexusSettings.NexusAppId);
        idInfo.AppInstanceId.Should().Be(nexusSettings.NexusAppInstanceId);
        
        epoch.Should().BeBefore(beforeIdGenerated);
        idInfo.CreatedAt.Should().BeBefore(afterIdGenerated);
        idInfo.CreatedAt.Should().BeAfter(beforeIdGenerated);
    }
}