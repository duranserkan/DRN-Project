using System.Collections.Concurrent;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Ids;
using DRN.Framework.Utils.Time;

namespace DRN.Test.Unit.Tests.Framework.Utils.Ids;

public class SourceKnownGenerationTimeTests
{
    private static readonly DateTimeOffset Epoch = SourceKnownGenerationTimePolicy.DefaultEpoch;
    private static readonly string[] UntrustedAssemblies = ["Nonexistent.Untrusted.Assembly"];
    private const long Precision = SourceKnownGenerationTimePolicy.PrecisionTicks;

    private static SourceKnownGenerationTimePolicy Policy(DateTimeOffset floor)
        => SourceKnownGenerationTimePolicy.Create().WithMinimumUtc(floor.ToString("O"));

    [Fact]
    public void Both_Default_Epoch_Halves_And_Entire_Final_Bucket_Are_Supported()
    {
        var policy = Policy(Epoch);
        var secondHalf = Epoch.AddTicks(SourceKnownIdUtils.TicksPerHalf * Precision);
        var finalBucket = Epoch.AddTicks(SourceKnownIdUtils.MaxEpochTicks * Precision);
        policy.Validate(Epoch, Epoch).Should().Be(0);
        policy.Validate(secondHalf.AddTicks(-1), Epoch).Should().Be(SourceKnownIdUtils.TicksPerHalf - 1);
        policy.Validate(secondHalf, Epoch).Should().Be(SourceKnownIdUtils.TicksPerHalf);
        policy.Validate(finalBucket.AddTicks(Precision - 1), Epoch).Should().Be(SourceKnownIdUtils.MaxEpochTicks);
        var beyond = () => policy.Validate(finalBucket.AddTicks(Precision), Epoch);
        beyond.Should().Throw<InvalidOperationException>().WithMessage("*after final timestamp bucket*");

        var firstId = EpochTimeUtils.ConvertToSourceKnownIdTimeStamp(Epoch, Epoch);
        var secondId = EpochTimeUtils.ConvertToSourceKnownIdTimeStamp(secondHalf, Epoch);
        firstId.Should().BeNegative();
        secondId.Should().BeGreaterThanOrEqualTo(0);
        SourceKnownIdUtils.ParseId(secondId, Epoch).CreatedAt.Should().Be(secondHalf);
    }

    [Fact]
    public void Floor_Is_Rounded_Up_And_Decoded_Time_Never_Precedes_It()
    {
        var floor = Epoch.AddTicks(Precision + 1);
        var policy = Policy(floor);
        var rounded = Epoch.AddTicks(2 * Precision);
        policy.GetMinimumUtc(Epoch).Should().Be(rounded);
        var below = () => policy.Validate(rounded.AddTicks(-1), Epoch);
        below.Should().Throw<InvalidOperationException>().WithMessage("*observed UTC=*minimum generation UTC=*configured override*epoch=0*below minimum*");
        var exactUnrounded = () => policy.Validate(floor, Epoch);
        exactUnrounded.Should().Throw<InvalidOperationException>();
        var timestamp = policy.Validate(rounded, Epoch);
        EpochTimeUtils.ConvertToDateTime(timestamp, Epoch).Should().BeOnOrAfter(floor);
        Policy(rounded).Validate(rounded, Epoch).Should().Be(2);
    }

    [Fact]
    public void Bounds_Use_Utc_And_Reject_SubBucket_PreEpoch_Times_And_Overflow()
    {
        var policy = Policy(Epoch);
        var before = () => policy.Validate(Epoch.AddTicks(-1), Epoch);
        before.Should().Throw<InvalidOperationException>().WithMessage("*before epoch*");
        var convertBefore = () => EpochTimeUtils.ConvertToSourceKnownIdTimeStamp(Epoch.AddTicks(-1), Epoch);
        convertBefore.Should().Throw<InvalidOperationException>();
        var offsetEpoch = Epoch.ToOffset(TimeSpan.FromHours(3));
        policy.Validate(Epoch.AddTicks(Precision), offsetEpoch).Should().Be(1);
        var customOrigin = Epoch.AddTicks(123);
        Policy(customOrigin.AddTicks(1)).GetMinimumUtc(customOrigin).Should().Be(customOrigin.AddTicks(Precision));
        var outsideFloor = Policy(Epoch.AddTicks(SourceKnownIdUtils.MaxEpochTicks * Precision + 1));
        var outside = () => outsideFloor.GetMinimumUtc(Epoch);
        outside.Should().Throw<InvalidOperationException>().WithMessage("*minimum outside representable epoch*");
        var nearLimit = Policy(DateTimeOffset.MaxValue);
        var overflowFloor = () => nearLimit.GetMinimumUtc(DateTimeOffset.MaxValue.AddTicks(-1));
        overflowFloor.Should().Throw<InvalidOperationException>();
        var overflowConversion = () => EpochTimeUtils.ConvertToDateTime(long.MaxValue, Epoch);
        overflowConversion.Should().Throw<OverflowException>();
    }

    [Fact]
    public void Default_Policy_Uses_The_Source_Minimum_And_Rejects_Earlier_Time()
    {
        var minimum = SourceKnownGenerationTimePolicy.MinimumGenerationUtc;
        minimum.Should().Be(new DateTimeOffset(2026, 9, 9, 0, 0, 0, TimeSpan.Zero));
        var policy = SourceKnownGenerationTimePolicy.Create();
        policy.MinimumUtc.Should().Be(minimum);
        policy.Sources.Should().Be("SharedKernel C# minimum");
        policy.Validate(minimum, Epoch).Should().Be(EpochTimeUtils.ConvertToTicks(minimum, Epoch));
        var below = () => policy.Validate(minimum.AddTicks(-1), Epoch);
        below.Should().Throw<InvalidOperationException>().WithMessage("*below minimum generation UTC*");
    }

    [Fact]
    public void Process_Policy_Uses_Code_Baseline_And_Is_Immutable()
    {
        SourceKnownGenerationTime.Policy.Should().BeSameAs(SourceKnownGenerationTime.ForGeneration);
        SourceKnownGenerationTimePolicy.Create().MinimumUtc.Should().Be(SourceKnownGenerationTime.Policy.MinimumUtc);
    }

    [Fact]
    public void Other_Assembly_And_Image_Settings_Cannot_Change_The_SharedKernel_Floor()
    {
        var policy = SourceKnownGenerationTime.Policy;
        using var settings = SettingsProvider.Development(new
        {
            SourceKnownIdSettings = new
            {
                ApplicationBuildUtc = "not UTC",
                TrustedAssemblies = UntrustedAssemblies
            }
        });
        _ = new SourceKnownIdUtils(settings);
        SourceKnownGenerationTime.Policy.Should().BeSameAs(policy);
    }

    [Fact]
    public void Explicit_Override_Replaces_The_Source_Minimum_In_Either_Direction()
    {
        var minimum = SourceKnownGenerationTimePolicy.MinimumGenerationUtc;
        var defaults = SourceKnownGenerationTimePolicy.Create();
        var earlier = minimum.AddDays(-1);
        var overridden = defaults.WithMinimumUtc(earlier.ToString("O"));
        overridden.MinimumUtc.Should().Be(earlier);
        overridden.Sources.Should().Be("configured override");
        overridden.Validate(earlier, Epoch).Should().Be(EpochTimeUtils.ConvertToTicks(earlier, Epoch));
        var defaultValidation = () => defaults.Validate(earlier, Epoch);
        defaultValidation.Should().Throw<InvalidOperationException>();
        defaults.WithMinimumUtc(minimum.AddDays(20).ToString("O")).MinimumUtc.Should().Be(minimum.AddDays(20));
        var malformed = () => defaults.WithMinimumUtc("");
        malformed.Should().Throw<Exception>().WithMessage("*configured override*ISO 8601 UTC*");
    }

    [Fact]
    public void Override_Freezes_Before_Generation_And_Cannot_Be_Changed_Later()
    {
        var state = new SourceKnownGenerationTimeState();
        var defaultPolicy = state.Initialize(null);
        var overrideUtc = SourceKnownGenerationTimePolicy.MinimumGenerationUtc.AddDays(-1);
        var overridden = state.Initialize(overrideUtc.ToString("O"));
        overridden.MinimumUtc.Should().Be(overrideUtc);
        state.GetForGeneration().Should().BeSameAs(overridden);
        state.Initialize(null).Should().BeSameAs(overridden);
        state.Initialize(overrideUtc.ToString("O")).Should().BeSameAs(overridden);
        var higher = () => state.Initialize(overrideUtc.AddDays(1).ToString("O"));
        var lower = () => state.Initialize(overrideUtc.AddDays(-1).ToString("O"));
        higher.Should().Throw<Exception>().WithMessage("*floor is frozen*");
        lower.Should().Throw<Exception>().WithMessage("*floor is frozen*");
        defaultPolicy.MinimumUtc.Should().BeOnOrAfter(SourceKnownGenerationTimePolicy.MinimumGenerationUtc);
        var earlyStatic = new SourceKnownGenerationTimeState();
        earlyStatic.GetForGeneration();
        var lateOverride = () => earlyStatic.Initialize(overrideUtc.ToString("O"));
        lateOverride.Should().Throw<Exception>().WithMessage("*before startup/first-use validation*");
    }

    [Theory]
    [DataInlineUnit("")]
    [DataInlineUnit("2026-09-09")]
    [DataInlineUnit("2026-09-09T12:00:00")]
    [DataInlineUnit("2026-09-09T12:00:00+03:00")]
    [DataInlineUnit("2026-02-30T00:00:00Z")]
    [DataInlineUnit("2026-09-09T12:00:00.12345678Z")]
    [DataInlineUnit("2026-09-09T12:00:00.Z")]
    public void Malformed_Or_NonUtc_Override_Is_Rejected(string text)
    {
        var parse = () => SourceKnownGenerationTimePolicy.Create().WithMinimumUtc(text);
        parse.Should().Throw<Exception>().WithMessage("*configured override*ISO 8601 UTC*");
    }

    [Fact]
    public void Explicit_Utc_Offset_And_Fractional_Override_Are_Preserved()
    {
        SourceKnownGenerationTimePolicy.ParseUtc("2026-01-01T00:00:00.1234567+00:00", "valid")
            .Should().Be(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(1234567));
    }

    [Fact]
    public void Validated_Cache_Rides_Through_Minor_Drift_And_Critical_Drift_Remains_Rejected()
    {
        var floor = Epoch.AddYears(2);
        var policy = Policy(floor);
        var previous = floor.AddSeconds(1).UtcTicks;
        var cached = new DateTimeOffset(previous, TimeSpan.Zero);
        var reads = 0;
        var initialization = new GenerationTimeInitialization(() => policy, () => { reads++; return cached; });
        initialization.EnsureInitialized();
        TimeStampManager.GetUpdatedTicks(previous, previous - Precision).Should().Be(previous);
        TimeStampManager.GetUpdatedTicks(previous, previous + Precision).Should().Be(previous + Precision);
        TimeStampManager.GetUpdatedTicks(previous, previous - 5 * TimeSpan.TicksPerSecond + 1).Should().Be(previous);
        var critical = () => TimeStampManager.GetUpdatedTicks(previous, previous - 5 * TimeSpan.TicksPerSecond);
        critical.Should().Throw<ClockDriftException>();
        cached = new DateTimeOffset(TimeStampManager.GetUpdatedTicks(previous, floor.AddTicks(-1).UtcTicks), TimeSpan.Zero);
        initialization.EnsureInitialized();
        reads.Should().Be(1);
        cached.Should().BeOnOrAfter(floor);
        var timestamp = initialization.GetTimestamp();
        initialization.Update();
        initialization.GetTimestamp().Should().Be(timestamp, "minor drift retains the cached UTC bucket");
        cached = new DateTimeOffset(TimeStampManager.GetUpdatedTicks(previous, previous + Precision), TimeSpan.Zero);
        initialization.Update();
        initialization.GetTimestamp().Should().Be(timestamp + 1);
    }

    [Fact]
    public void Failed_Initialization_Is_Retryable_And_Success_Is_Reused()
    {
        var floor = Epoch.AddYears(2);
        var policy = Policy(floor);
        var cached = floor.AddTicks(-1);
        var reads = 0;
        var initialization = new GenerationTimeInitialization(() => policy, () => { reads++; return cached; });
        var initialize = () => initialization.EnsureInitialized();
        initialize.Should().Throw<InvalidOperationException>().WithMessage("*below minimum*");
        cached = floor;
        initialize.Should().NotThrow();
        initialize.Should().NotThrow();
        reads.Should().Be(2, "successful validation is reused after the initial failed attempt");
    }

    [Fact]
    public void Concurrent_First_Use_Validates_The_Default_Epoch_Once()
    {
        var policy = Policy(Epoch);
        var reads = 0;
        var initialization = new GenerationTimeInitialization(() => policy, () =>
        {
            Interlocked.Increment(ref reads);
            return Epoch.AddDays(1);
        });
        Parallel.For(0, 128, _ => initialization.EnsureInitialized());
        reads.Should().Be(1);
    }

    [Fact]
    public void Concurrent_Cache_Reads_And_Updates_Should_Preserve_Monotonic_Timestamps()
    {
        var utcTicks = Epoch.UtcTicks;
        var initialization = new GenerationTimeInitialization(() => Policy(Epoch),
            () => new DateTimeOffset(Volatile.Read(ref utcTicks), TimeSpan.Zero));
        Parallel.Invoke(
            () =>
            {
                for (var timestamp = 1; timestamp <= 1_000; timestamp++)
                {
                    Volatile.Write(ref utcTicks, Epoch.UtcTicks + timestamp * Precision);
                    initialization.Update();
                }
            },
            () =>
            {
                var previous = initialization.GetTimestamp();
                for (var read = 0; read < 10_000; read++)
                {
                    var current = initialization.GetTimestamp();
                    current.Should().BeGreaterThanOrEqualTo(previous);
                    previous = current;
                }
            });
        // An update racing first initialization may defer publication until the next updater pass.
        initialization.Update();
        initialization.GetTimestamp().Should().Be(1_000);
    }

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public void Cached_Generation_Should_Publish_Epoch_Failure_Instead_Of_Stale_Time(bool beforeEpoch)
    {
        var finalTick = Epoch.UtcTicks + SourceKnownGenerationTimePolicy.TimestampsPerEpoch * Precision - 1;
        var cached = new DateTimeOffset(finalTick, TimeSpan.Zero);
        var initialization = new GenerationTimeInitialization(() => Policy(Epoch), () => cached);
        initialization.GetTimestamp().Should().Be(SourceKnownGenerationTimePolicy.MaxTimestamp);
        cached = new DateTimeOffset(beforeEpoch ? Epoch.UtcTicks - 1 : finalTick + 1, TimeSpan.Zero);
        initialization.Update();
        var read = () => initialization.GetTimestamp();
        read.Should().Throw<InvalidOperationException>().WithMessage("*outside*supported*epoch*");
        initialization.Update();
        read.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cache_Updates_Should_Not_Freeze_Policy_Before_First_Generation()
    {
        var state = new SourceKnownGenerationTimeState();
        var policyReads = 0;
        var utcReads = 0;
        var epoch = Epoch.AddTicks(123);
        var cached = epoch.AddTicks(2 * Precision);
        var initialization = new GenerationTimeInitialization(
            () => { policyReads++; return state.GetForGeneration(); },
            () => { utcReads++; return cached; });
        initialization.Update();
        policyReads.Should().Be(0);
        utcReads.Should().Be(0);

        state.Initialize(epoch.ToString("O"), epoch.ToString("O"));
        initialization.GetTimestamp().Should().Be(2);
        initialization.GetTimestamp().Should().Be(2);
        policyReads.Should().Be(1);
        utcReads.Should().Be(1, "cached reads do not revisit UTC or the epoch");

        cached = cached.AddTicks(Precision);
        initialization.GetTimestamp().Should().Be(2, "only the updater publishes the next bucket");
        initialization.Update();
        initialization.GetTimestamp().Should().Be(3);
        policyReads.Should().Be(1);
        utcReads.Should().Be(2);
        initialization.Update();
        initialization.GetTimestamp().Should().Be(3, "unchanged UTC must retain the same bucket");
    }

    [Fact]
    public void Concurrent_Generation_Across_Bucket_Rollovers_Does_Not_Duplicate_Ids()
    {
        var ids = new ConcurrentBag<long>();
        for (var round = 0; round < 3; round++)
        {
            var previousTimestamp = SequenceManager<OriginSwitchEntity>.GetTimeScopedId().TimeStamp;
            SpinWait.SpinUntil(() => TimeStampManager.CurrentTimestamp() > previousTimestamp, TimeSpan.FromSeconds(5))
                .Should().BeTrue("the next batch must exercise scope rollover");
            Parallel.For(0, 3_000, _ => ids.Add(SourceKnownIdUtils.Generate<OriginSwitchEntity>(1, 1)));
        }
        ids.Count.Should().Be(9_000);
        ids.Distinct().Count().Should().Be(ids.Count);
    }

    private sealed class OriginSwitchEntity : SourceKnownEntity;

    [Fact]
    public void Configured_Epoch_Requires_Minimum_And_Freezes_With_It()
    {
        var state = new SourceKnownGenerationTimeState();
        var epoch = Epoch.AddDays(1);
        var missingMinimum = () => state.Initialize(null, epoch.ToString("O"));
        missingMinimum.Should().Throw<Exception>().WithMessage("*DefaultEpoch*requires*MinimumUtc*");
        var malformedEpoch = () => state.Initialize(Epoch.ToString("O"), "not UTC");
        malformedEpoch.Should().Throw<Exception>().WithMessage("*configured default epoch*ISO 8601 UTC*");
        state.Initialize(null).Epoch.Should().Be(Epoch, "invalid pairs must not partially change the policy");
        var configured = state.Initialize(epoch.ToString("O"), epoch.ToString("O"));
        configured.Epoch.Should().Be(epoch);
        configured.Validate(epoch).Should().Be(0);
        state.GetForGeneration().Should().BeSameAs(configured);
        state.Initialize(epoch.ToString("O"), epoch.ToString("O")).Should().BeSameAs(configured);
        state.Initialize(null).Should().BeSameAs(configured);
        var changedEpoch = () => state.Initialize(epoch.ToString("O"), Epoch.ToString("O"));
        changedEpoch.Should().Throw<Exception>().WithMessage("*epoch is frozen*");
        var changedMinimum = () => state.Initialize(epoch.AddDays(1).ToString("O"));
        changedMinimum.Should().Throw<Exception>().WithMessage("*floor is frozen*");
    }
}
