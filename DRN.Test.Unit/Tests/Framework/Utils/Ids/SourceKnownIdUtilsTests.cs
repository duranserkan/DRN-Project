using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Ids;
using DRN.Framework.Utils.Time;

namespace DRN.Test.Unit.Tests.Framework.Utils.Ids;

public readonly struct CustomUtilsTestApp : IAppId
{
    public const byte Value = 77;
    public static byte AppId => Value;
}

[EntityType<CustomUtilsTestApp>(1)]
public class CustomTestEntityForUtils : SourceKnownEntity;

[SuppressMessage("ReSharper", "RedundantCast")]
public class SourceKnownIdUtilsTests
{
    [Fact]
    public async Task SourceKnownIdUtils_Generate_Should_Generate_Valid_Id()
    {
        byte appId = 1;
        byte appInstanceId = 1;

        var epoch = EpochTimeUtils.Epoch2025;
        var beforeIdGenerated = DateTimeOffset.UtcNow;

        await Task.Delay(1200);
        var id = SourceKnownIdUtils.Generate<CustomTestEntityForUtils>(appId, appInstanceId);
        await Task.Delay(1200);

        var afterIdGenerated = DateTimeOffset.UtcNow;
        var idInfo = SourceKnownIdUtils.ParseId(id, EpochTimeUtils.DefaultEpoch);

        idInfo.Id.Should().Be(id);
        idInfo.AppId.Should().Be(appId);
        idInfo.AppInstanceId.Should().Be(appInstanceId);

        epoch.Should().BeBefore(beforeIdGenerated);
        AssertCreatedAtWithinGeneratedRange(idInfo, beforeIdGenerated, afterIdGenerated, epoch);
    }

    [Theory]
    [DataInlineUnit]
    public async Task SourceKnownIdUtils_Should_Generate_Next_Valid_Id(DrnTestContextUnit context)
    {
        var nexusSettings = new NexusAppSettings
        {
            AppId = 5,
            AppInstanceId = 12
        };

        var customSettings = new
        {
            NexusAppSettings = nexusSettings
        };

        context.AddToConfiguration(customSettings);
        var generator = context.GetRequiredService<ISourceKnownIdUtils>();

        var epoch = EpochTimeUtils.Epoch2025;
        var beforeIdGenerated = DateTimeOffset.UtcNow;

        await Task.Delay(1100); // 100ms buffer added to compensate caching effect
        var id1 = generator.Next<CustomTestEntityForUtils>();
        await Task.Delay(1100);

        var afterIdGenerated = DateTimeOffset.UtcNow;

        id1.Should().BeNegative();
        epoch.Should().BeBefore(beforeIdGenerated);

        var idInfo1 = generator.Parse(id1);
        var idInfo1Duplicate = generator.Parse(id1);
        idInfo1.AppId.Should().Be(77);
        idInfo1.AppInstanceId.Should().Be(nexusSettings.AppInstanceId);

        AssertCreatedAtWithinGeneratedRange(idInfo1, beforeIdGenerated, afterIdGenerated, epoch);

        var id2 = generator.Next<CustomTestEntityForUtils>();
        (id2 > id1).Should().BeTrue();

        var idInfo2 = generator.Parse(id2);
        var idInfo2Duplicate = generator.Parse(id2);
        (idInfo2 > idInfo1).Should().BeTrue();
        (idInfo2 >= idInfo1).Should().BeTrue();
        (idInfo2 >= idInfo2Duplicate).Should().BeTrue();
        (idInfo1 < idInfo2).Should().BeTrue();
        (idInfo1 <= idInfo2).Should().BeTrue();
        (idInfo1 <= idInfo1Duplicate).Should().BeTrue();
    }

    [Theory]
    [InlineData((byte)128, (byte)0)]
    [InlineData((byte)255, (byte)0)]
    public void Generate_With_AppId_Exceeding_MaxAppId_Should_Throw_ArgumentOutOfRangeException(byte appId, byte appInstanceId)
    {
        var act = () => SourceKnownIdUtils.Generate<CustomTestEntityForUtils>(appId, appInstanceId);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData((byte)0, (byte)64)]
    [InlineData((byte)0, (byte)255)]
    public void Generate_With_AppInstanceId_Exceeding_MaxAppInstanceId_Should_Throw_ArgumentOutOfRangeException(byte appId, byte appInstanceId)
    {
        var act = () => SourceKnownIdUtils.Generate<CustomTestEntityForUtils>(appId, appInstanceId);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [DataInlineUnit]
    public void Next_And_Parse_Should_Honor_Custom_Epoch(DrnTestContextUnit context)
    {
        var generator = context.GetRequiredService<ISourceKnownIdUtils>();
        var customEpoch = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var before = TimeStampManager.UtcNow;
        var id = generator.Next<CustomTestEntityForUtils>(appId: 10, appInstanceId: 5, epoch: customEpoch);
        var after = TimeStampManager.UtcNow;
        var parsed = generator.Parse(id, epoch: customEpoch);

        parsed.Id.Should().Be(id);
        parsed.AppId.Should().Be(10);
        parsed.AppInstanceId.Should().Be(5);
        AssertCreatedAtWithinGeneratedRange(parsed, before, after, customEpoch);
    }

    [Theory]
    [DataInlineUnit((byte)128, (byte)1)]
    [DataInlineUnit((byte)1, (byte)64)]
    public void Constructor_With_Invalid_AppId_Or_AppInstanceId_In_Settings_Should_Throw(
        DrnTestContextUnit context, byte appId, byte appInstanceId)
    {
        var invalidSettings = new
        {
            NexusAppSettings = new NexusAppSettings
            {
                AppId = appId,
                AppInstanceId = appInstanceId
            }
        };

        context.AddToConfiguration(invalidSettings);
        var act = () => context.GetRequiredService<ISourceKnownIdUtils>();
        act.Should().Throw<ValidationException>();
    }

    [Theory]
    [DataInlineUnit]
    public void Next_With_SourceKnownEntity_Should_Use_Entity_AppId(DrnTestContextUnit context)
    {
        var nexusSettings = new NexusAppSettings
        {
            AppId = 5,
            AppInstanceId = 12
        };

        context.AddToConfiguration(new { NexusAppSettings = nexusSettings });
        var generator = context.GetRequiredService<ISourceKnownIdUtils>();

        var id = generator.Next<CustomTestEntityForUtils>();
        var parsed = generator.Parse(id);

        parsed.AppId.Should().Be(77);
        parsed.AppInstanceId.Should().Be(12);
    }

    [Theory]
    [DataInlineUnit]
    public void Next_WithEntity_Should_Generate_Valid_Id_Matching_Generic_Contract(DrnTestContextUnit context)
    {
        var nexusSettings = new NexusAppSettings
        {
            AppId = 8,
            AppInstanceId = 20
        };

        context.AddToConfiguration(new { NexusAppSettings = nexusSettings });
        var generator = context.GetRequiredService<ISourceKnownIdUtils>();

        var idGeneric = generator.Next<CustomTestEntityForUtils>();
        var entity = new CustomTestEntityForUtils();
        var idEntity = generator.Next(entity);

        var parsedGeneric = generator.Parse(idGeneric);
        var parsedEntity = generator.Parse(idEntity);

        parsedEntity.AppId.Should().Be(77);
        parsedEntity.AppInstanceId.Should().Be(nexusSettings.AppInstanceId);
        parsedGeneric.AppId.Should().Be(77);
        parsedGeneric.AppInstanceId.Should().Be(nexusSettings.AppInstanceId);
    }

    [Theory]
    [DataInlineUnit]
    public void Next_WithEntity_With_SourceKnownEntity_Should_Use_Entity_AppId(DrnTestContextUnit context)
    {
        var nexusSettings = new NexusAppSettings
        {
            AppId = 5,
            AppInstanceId = 12
        };

        context.AddToConfiguration(new { NexusAppSettings = nexusSettings });
        var generator = context.GetRequiredService<ISourceKnownIdUtils>();

        var entity = new CustomTestEntityForUtils();
        var id = generator.Next(entity);
        var parsed = generator.Parse(id);

        parsed.AppId.Should().Be(77);
        parsed.AppInstanceId.Should().Be(12);
    }

    [Theory]
    [DataInlineUnit]
    public void Next_WithType_And_Explicit_Parameters_Should_Honor_Parameters(DrnTestContextUnit context)
    {
        var generator = context.GetRequiredService<ISourceKnownIdUtils>();
        var customEpoch = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var id = generator.Next(typeof(CustomTestEntityForUtils), appId: 42, appInstanceId: 18, epoch: customEpoch);
        var parsed = generator.Parse(id, epoch: customEpoch);

        parsed.Id.Should().Be(id);
        parsed.AppId.Should().Be(42);
        parsed.AppInstanceId.Should().Be(18);
    }

    [Fact]
    public void Generate_WithType_Should_Generate_Valid_Id()
    {
        var entityType = typeof(CustomTestEntityForUtils);
        byte appId = 33;
        byte appInstanceId = 7;
        var epoch = EpochTimeUtils.Epoch2025;

        var id = SourceKnownIdUtils.Generate(entityType, appId, appInstanceId, epoch);
        var parsed = SourceKnownIdUtils.ParseId(id, epoch);

        parsed.Id.Should().Be(id);
        parsed.AppId.Should().Be(appId);
        parsed.AppInstanceId.Should().Be(appInstanceId);
    }

    [Theory]
    [DataInlineUnit]
    public void Next_WithType_With_NonEntity_Type_Should_Throw_ArgumentException(DrnTestContextUnit context)
    {
        var generator = context.GetRequiredService<ISourceKnownIdUtils>();

        // Value types must throw ArgumentException
        var actValueTypeExplicit = () => generator.Next(typeof(int), 1, 1);
        actValueTypeExplicit.Should().Throw<ArgumentException>();

        var actValueTypeGenerate = () => SourceKnownIdUtils.Generate(typeof(int), 1, 1);
        actValueTypeGenerate.Should().Throw<ArgumentException>();

        // Non-entity class types must throw ArgumentException on Next(Type, ...) and Generate(Type, ...)
        var actNonEntityClassExplicit = () => generator.Next(typeof(SourceKnownIdUtilsTests), 1, 1);
        actNonEntityClassExplicit.Should().Throw<ArgumentException>()
            .WithMessage($"Type '{typeof(SourceKnownIdUtilsTests).FullName}' must inherit from '{nameof(SourceKnownEntity)}'.*");

        var actNonEntityClassGenerate = () => SourceKnownIdUtils.Generate(typeof(SourceKnownIdUtilsTests), 1, 1);
        actNonEntityClassGenerate.Should().Throw<ArgumentException>()
            .WithMessage($"Type '{typeof(SourceKnownIdUtilsTests).FullName}' must inherit from '{nameof(SourceKnownEntity)}'.*");
    }

    [Theory]
    [DataInlineUnit]
    public void Next_With_Null_Entity_Or_EntityType_Should_Throw_ArgumentNullException(DrnTestContextUnit context)
    {
        var generator = context.GetRequiredService<ISourceKnownIdUtils>();

        var actNextEntity = () => generator.Next((SourceKnownEntity)null!);
        actNextEntity.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("entity");

        var actNextExplicit = () => generator.Next((Type)null!, 1, 1);
        actNextExplicit.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("entityType");

        var actGenerate = () => SourceKnownIdUtils.Generate(null!, 1, 1);
        actGenerate.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("entityType");
    }

    [Theory]
    [DataInlineUnit]
    public void Next_WithEntity_Should_Be_ThreadSafe_Under_Concurrent_Load(DrnTestContextUnit context)
    {
        var generator = context.GetRequiredService<ISourceKnownIdUtils>();
        const int threadCount = 8;
        const int iterationsPerThread = 500;
        var generatedIds = new ConcurrentBag<long>();
        var entity = new CustomTestEntityForUtils();

        Parallel.For(0, threadCount, _ =>
        {
            for (var i = 0; i < iterationsPerThread; i++)
            {
                var id = generator.Next(entity);
                generatedIds.Add(id);
            }
        });

        generatedIds.Count.Should().Be(threadCount * iterationsPerThread);
        generatedIds.Distinct().Count().Should().Be(threadCount * iterationsPerThread);
    }

    [Theory]
    [DataInlineUnit]
    public void Warmup_With_EntityTypes_Should_Precompile_Delegates_And_Generate_Valid_Ids(DrnTestContextUnit context)
    {
        var generator = context.GetRequiredService<ISourceKnownIdUtils>();

        var actWarmup = () => SourceKnownIdUtils.Warmup([typeof(CustomTestEntityForUtils), typeof(SourceKnownIdUtilsTests)]);
        actWarmup.Should().NotThrow();

        // Warmup via interface default implementation
        var actInterfaceWarmup = () => generator.Warmup([typeof(CustomTestEntityForUtils)]);
        actInterfaceWarmup.Should().NotThrow();

        // Verify ID generation after warmup
        var entity = new CustomTestEntityForUtils();
        var id = generator.Next(entity);
        var parsed = generator.Parse(id);
        parsed.AppId.Should().Be(77);

        var entityType = typeof(CustomTestEntityForUtils);
        var generated = SourceKnownIdUtils.Generate(entityType, 10, 5);
        var parsedGenerated = SourceKnownIdUtils.ParseId(generated, EpochTimeUtils.DefaultEpoch);
        parsedGenerated.AppId.Should().Be(10);
        parsedGenerated.AppInstanceId.Should().Be(5);
    }

    [Fact]
    public void Warmup_With_Null_Should_Throw_ArgumentNullException()
    {
        var act = () => SourceKnownIdUtils.Warmup(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Warmup_With_Mixed_Nulls_And_ValueTypes_Should_Safely_Ignore_Them()
    {
        var act = () => SourceKnownIdUtils.Warmup([null!, typeof(int), typeof(SourceKnownIdUtilsTests), typeof(CustomTestEntityForUtils)]);
        act.Should().NotThrow();
    }

    /// <summary>
    /// Validates that <paramref name="idInfo"/>.CreatedAt falls within the expected test execution range.
    /// Timestamps are converted to 250ms epoch ticks before assertion because TimeStampManager truncates
    /// timestamps to 250ms precision boundaries. Callers without an intentional time buffer must capture
    /// bounds from TimeStampManager.UtcNow; converting live wall-clock bounds alone cannot prevent a race
    /// with the periodically refreshed cache.
    /// </summary>
    private static void AssertCreatedAtWithinGeneratedRange(
        SourceKnownId idInfo,
        DateTimeOffset beforeIdGenerated,
        DateTimeOffset afterIdGenerated,
        DateTimeOffset epoch)
    {
        var createdAtTimestamp = EpochTimeUtils.ConvertToTicks(idInfo.CreatedAt, epoch);
        createdAtTimestamp.Should().BeGreaterThanOrEqualTo(EpochTimeUtils.ConvertToTicks(beforeIdGenerated, epoch));
        createdAtTimestamp.Should().BeLessThanOrEqualTo(EpochTimeUtils.ConvertToTicks(afterIdGenerated, epoch));
    }
}
