using System.Globalization;
using System.Text.RegularExpressions;

namespace DRN.Framework.SharedKernel.Domain;

/// <summary>An immutable UTC lower bound. Historical decoding must not use this policy.</summary>
public sealed class SourceKnownGenerationTimePolicy
{
    private static readonly string[] UtcFormats =
    [
        "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'",
        "yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz"
    ];

    public const long PrecisionTicks = TimeSpan.TicksPerMillisecond * 250;
    // Encoding capacity is fixed independently of the supported epoch/half limits.
    public const int TimestampBitsPerHalf = 32;
    public const long TimestampsPerHalf = 1L << TimestampBitsPerHalf;
    public const long TimestampsPerEpoch = TimestampsPerHalf * 2;
    public const long MaxTimestamp = TimestampsPerEpoch - 1;

    /// <summary>Highest supported encoded epoch. Only epoch 0 is currently implemented.</summary>
    public const byte MaxSupportedEpoch = 0;
    /// <summary>Highest supported zero-based epoch half: 0 is the first half, 1 is the second.</summary>
    public const byte MaxSupportedEpochHalf = 1;
    public static readonly DateTimeOffset DefaultEpoch = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Developer-maintained minimum, independent of build inputs. Advance deliberately; never derive from the runtime clock.</summary>
    public static readonly DateTimeOffset MinimumGenerationUtc = new(2026, 9, 9, 0, 0, 0, TimeSpan.Zero);

    public DateTimeOffset MinimumUtc { get; }
    public DateTimeOffset Epoch { get; }
    public string Sources { get; }

    private SourceKnownGenerationTimePolicy(DateTimeOffset minimumUtc, DateTimeOffset epoch, string sources)
    {
        MinimumUtc = minimumUtc;
        Epoch = epoch;
        Sources = sources;
    }

    public static SourceKnownGenerationTimePolicy Create()
        => new(MinimumGenerationUtc, DefaultEpoch, "SharedKernel C# minimum");

    internal SourceKnownGenerationTimePolicy WithMinimumUtc(string value)
        => new(ParseUtc(value, "configured override"), Epoch, "configured override");

    internal SourceKnownGenerationTimePolicy WithConfiguration(string? minimumUtc, string? defaultEpoch)
    {
        if (defaultEpoch != null && minimumUtc == null)
            throw ExceptionFor.Configuration("SourceKnownIdSettings:DefaultEpoch requires an explicit SourceKnownIdSettings:MinimumUtc.");
        var candidate = new SourceKnownGenerationTimePolicy(
            minimumUtc == null ? MinimumUtc : ParseUtc(minimumUtc, "configured override"),
            defaultEpoch == null ? Epoch : ParseUtc(defaultEpoch, "configured default epoch"),
            "configured override");
        _ = candidate.GetMinimumUtc();
        return candidate;
    }

    /// <summary>Accepts ISO 8601 UTC, explicitly suffixed with Z or +00:00; never assumes local time.</summary>
    public static DateTimeOffset ParseUtc(string value, string source)
    {
        if (string.IsNullOrEmpty(value) ||
            !Regex.IsMatch(value, @"\A[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,7})?(Z|\+00:00)\z") ||
            !DateTimeOffset.TryParseExact(value, UtcFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var utc) || utc.Offset != TimeSpan.Zero)
            throw ExceptionFor.Configuration($"Source-Known {source} must be an ISO 8601 UTC timestamp with Z or +00:00.");
        return utc;
    }

    public DateTimeOffset GetMinimumUtc() => GetMinimumUtc(Epoch, MinimumUtc);

    internal DateTimeOffset GetMinimumUtc(DateTimeOffset epoch)
        => GetMinimumUtc(epoch, MinimumUtc);

    private DateTimeOffset GetMinimumUtc(DateTimeOffset epoch, DateTimeOffset observedUtc)
    {
        var delta = MinimumUtc.UtcTicks - epoch.UtcTicks;
        // A baseline before the origin does not weaken the origin's own lower bound.
        var units = delta <= 0 ? 0 : (delta + PrecisionTicks - 1) / PrecisionTicks;
        if (units > MaxTimestamp || epoch.UtcTicks > DateTimeOffset.MaxValue.UtcTicks - units * PrecisionTicks)
            throw Failure(observedUtc, epoch, "minimum outside representable epoch");
        return new DateTimeOffset(epoch.UtcTicks + units * PrecisionTicks, TimeSpan.Zero);
    }

    /// <summary>Validates actual UTC or a cached generation time and returns the encoded 250ms timestamp.</summary>
    public long Validate(DateTimeOffset observedUtc) => Validate(observedUtc, Epoch);

    internal long Validate(DateTimeOffset observedUtc, DateTimeOffset epoch)
    {
        var minimum = GetMinimumUtc(epoch, observedUtc);
        var elapsed = observedUtc.UtcTicks - epoch.UtcTicks;
        if (elapsed < 0)
            throw Failure(observedUtc, epoch, "before epoch", minimum);
        if (elapsed >= (MaxTimestamp + 1) * PrecisionTicks)
            throw Failure(observedUtc, epoch, "after final timestamp bucket", minimum);
        if (observedUtc < minimum)
            throw Failure(observedUtc, epoch, "below minimum generation UTC", minimum);
        return elapsed / PrecisionTicks;
    }

    private InvalidOperationException Failure(DateTimeOffset observed, DateTimeOffset epoch, string boundary,
        DateTimeOffset? minimum = null) => new(
        $"Source-Known generation rejected: observed UTC={observed:O}; minimum generation UTC={minimum ?? MinimumUtc:O}; " +
        $"sources={Sources}; epoch={MaxSupportedEpoch}, origin UTC={epoch.ToUniversalTime():O}; boundary={boundary}.");
}

/// <summary>Process-wide policy shared by static, non-hosted and hosted ID generation.</summary>
public static class SourceKnownGenerationTime
{
    private static readonly SourceKnownGenerationTimeState State = new();

    /// <summary>An explicit override replaces the SharedKernel C# minimum. Set it before startup/first-use validation.</summary>
    public static SourceKnownGenerationTimePolicy Initialize(string? minimumUtc = null, string? defaultEpoch = null)
        => State.Initialize(minimumUtc, defaultEpoch);

    public static SourceKnownGenerationTimePolicy Policy => State.Initialize(null);
    internal static SourceKnownGenerationTimePolicy ForGeneration => State.GetForGeneration();
    /// <summary>The process-wide origin. Reading it freezes epoch and minimum to preserve historical interpretation.</summary>
    public static DateTimeOffset Epoch => State.GetForGeneration().Epoch;
}

internal sealed class SourceKnownGenerationTimeState
{
    private readonly Lock _sync = new();
    private SourceKnownGenerationTimePolicy? _policy;
    private volatile bool _generationStarted;

    internal SourceKnownGenerationTimePolicy Initialize(string? minimumUtc, string? defaultEpoch = null)
    {
        lock (_sync)
        {
            _policy ??= SourceKnownGenerationTimePolicy.Create();
            if (minimumUtc == null && defaultEpoch == null)
                return _policy;
            var candidate = _policy.WithConfiguration(minimumUtc, defaultEpoch);
            if (_generationStarted)
            {
                if (candidate.MinimumUtc != _policy.MinimumUtc || candidate.Epoch != _policy.Epoch)
                    throw ExceptionFor.Configuration($"Source-Known floor is frozen at {_policy.MinimumUtc:O} ({_policy.Sources}); " +
                        $"epoch is frozen at {_policy.Epoch:O}; requested minimum {candidate.MinimumUtc:O}, epoch {candidate.Epoch:O}. " +
                        "Set SourceKnownIdSettings:MinimumUtc and DefaultEpoch before startup/first-use validation or historical conversion.");
                return _policy;
            }
            return _policy = candidate;
        }
    }

    internal SourceKnownGenerationTimePolicy GetForGeneration()
    {
        if (_generationStarted)
            return _policy!;
        lock (_sync)
        {
            _policy ??= SourceKnownGenerationTimePolicy.Create();
            _generationStarted = true;
            return _policy;
        }
    }
}
