using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Numbers;
using DRN.Framework.Utils.Settings;
using DRN.Framework.Utils.Time;

namespace DRN.Framework.Utils.Ids;

public interface ISourceKnownIdUtils
{
    /// <summary>
    /// Generates Ids for the entity.
    /// Resolves appId from the entity's [EntityType] attribute, and appInstanceId from appsettings.
    /// Uses <see cref="IEpochTimeUtils.Epoch"/>"
    /// </summary>
    /// <typeparam name="TEntity">The entity type for which Ids are generated. Must derive from <see cref="SourceKnownEntity"/>.</typeparam>
    long Next<TEntity>() where TEntity : SourceKnownEntity;

    long Next<TEntity>(byte appId, byte appInstanceId) where TEntity : SourceKnownEntity;

    /// <summary>
    /// Generates Ids for the specified entity.
    /// Resolves appId from the entity's [EntityType] attribute, and appInstanceId from appsettings.
    /// Uses <see cref="IEpochTimeUtils.Epoch"/>
    /// </summary>
    /// <param name="entity">The entity for which Ids are generated. Must derive from <see cref="SourceKnownEntity"/>.</param>
    long Next(SourceKnownEntity entity);

    /// <summary>
    /// Generates Ids for the specified entity type using the provided appId and appInstanceId.
    /// </summary>
    /// <param name="entityType">The entity type for which Ids are generated. Must derive from <see cref="SourceKnownEntity"/>.</param>
    /// <param name="appId">Application Identifier (0..127)</param>
    /// <param name="appInstanceId">Application Instance Identifier (0..63)</param>
    long Next(Type entityType, byte appId, byte appInstanceId);

    /// <summary>
    /// Pre-compiles and warms up ID generation delegates for the specified entity types.
    /// Eliminates cold-start JIT and reflection overhead during application startup.
    /// </summary>
    /// <param name="entityTypes">Collection of entity class types to warm up.</param>
    void Warmup(ICollection<Type> entityTypes) => SourceKnownIdUtils.Warmup(entityTypes);

    SourceKnownId Parse(long id);
}

[Singleton<ISourceKnownIdUtils>]
[SuppressMessage("ReSharper", "InconsistentlySynchronizedField")]
[SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract")]
public class SourceKnownIdUtils : ISourceKnownIdUtils
{
    public SourceKnownIdUtils(IAppSettings appSettings)
    {
        SourceKnownIdSettings.Initialize(appSettings.Configuration);
        _nexusAppInstanceId = ValidateAppInstanceId(appSettings.NexusAppSettings.AppInstanceId);
    }

    public const byte MaxAppId = IAppId.MaxAppId;
    public const byte MaxAppInstanceId = 63;
    public const long TicksPerHalf = SourceKnownGenerationTimePolicy.TimestampsPerHalf; // 2^32 ticks per half-epoch
    public const long MaxEpochTicks = SourceKnownGenerationTimePolicy.MaxTimestamp; // 2^33 - 1: both halves

    private sealed class DelegateCacheSnapshot(
        FrozenDictionary<Type, Func<byte, byte, long>> frozenDelegates,
        ConcurrentDictionary<Type, Func<byte, byte, long>> dynamicDelegates)
    {
        public readonly FrozenDictionary<Type, Func<byte, byte, long>> FrozenDelegates = frozenDelegates;
        public readonly ConcurrentDictionary<Type, Func<byte, byte, long>> DynamicDelegates = dynamicDelegates;
    }

    private static readonly Lock SyncLock = new();

    private static volatile DelegateCacheSnapshot _delegateCache = new(
        FrozenDictionary<Type, Func<byte, byte, long>>.Empty,
        new ConcurrentDictionary<Type, Func<byte, byte, long>>());

    private static readonly MethodInfo GenerateGenericMethodDefinition = typeof(SourceKnownIdUtils)
        .GetMethods(BindingFlag.StaticPublic)
        .First(m => m is { Name: nameof(Generate), IsGenericMethodDefinition: true } && m.GetParameters().Length == 2);

    /// <summary>
    /// Pre-compiles and warms up ID generation delegates for the specified entity types.
    /// Eliminates cold-start JIT and reflection overhead during application startup.
    /// </summary>
    /// <param name="entityTypes">Collection of entity class types to warm up.</param>
    [SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract")]
    public static void Warmup(ICollection<Type> entityTypes)
    {
        ArgumentNullException.ThrowIfNull(entityTypes);
        if (entityTypes.Count == 0)
            return;

        lock (SyncLock)
        {
            var current = _delegateCache;
            if (current.DynamicDelegates.IsEmpty && AllWarmedUp(current.FrozenDelegates, entityTypes))
                return;

            var map = new Dictionary<Type, Func<byte, byte, long>>(current.FrozenDelegates);
            var anyNew = false;

            // Merge any previously recorded dynamic entries
            foreach (var (k, v) in current.DynamicDelegates)
            {
                map[k] = v;
                anyNew = true;
            }

            foreach (var type in entityTypes)
            {
                if (type is null || !type.IsClass || !typeof(SourceKnownEntity).IsAssignableFrom(type))
                    continue;

                if (map.ContainsKey(type))
                    continue;

                anyNew = true;
                map[type] = CreateGenerateDelegate(type);
            }

            if (!anyNew && current.FrozenDelegates.Count > 0)
                return;

            _delegateCache = new DelegateCacheSnapshot(
                map.ToFrozenDictionary(),
                new ConcurrentDictionary<Type, Func<byte, byte, long>>());
        }
    }

    private static bool AllWarmedUp(FrozenDictionary<Type, Func<byte, byte, long>> frozen, ICollection<Type> types)
    {
        if (frozen.Count == 0 || types.Count == 0) return false;
        foreach (var type in types)
        {
            if (type is not null && type.IsClass && typeof(SourceKnownEntity).IsAssignableFrom(type) && !frozen.ContainsKey(type))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Generates a numeric 64-bit Source-Known ID for the specified entity type using explicit partition and instance identifiers.
    /// </summary>
    /// <typeparam name="TEntity">The entity type for which IDs are generated. Must derive from <see cref="SourceKnownEntity"/>.</typeparam>
    /// <param name="appId">Application Identifier (0..127)</param>
    /// <param name="appInstanceId">Application Instance Identifier (0..63)</param>
    public static long Generate<TEntity>(byte appId, byte appInstanceId) where TEntity : SourceKnownEntity
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(appId, MaxAppId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(appInstanceId, MaxAppInstanceId);

        var builder = NumberBuilder.GetLong();
        var timeScopedId = SequenceManager<TEntity>.GetTimeScopedId();

        //Timestamp with 250ms precision (4 ticks per second)
        //Sub-second ordering eliminates coarse-grained temporal ambiguity while preserving throughput.
        // SequenceManager validates the sampled UTC against the epoch range before allocating a sequence.

        //Epoch half determination: 32-bit timestamp, sign bit selects half
        //First half (ticks < 2^32): sign=1, negative SKID. Second half (ticks ≥ 2^32): sign=0, positive SKID.
        //Negative sorts before positive, preserving monotonic ordering across the full ~68-year epoch.
        var isSecondHalf = timeScopedId.TimeStamp >= TicksPerHalf;
        var storedTimestamp = (uint)(timeScopedId.TimeStamp & uint.MaxValue); // Mask to 32 bits
        builder.SetResidueValue(storedTimestamp);
        if (isSecondHalf)
            builder.MakePositive();

        //128 apps (7 bits) — sufficient for any application topology
        builder.TryAdd(appId, 7);

        //64 app instances per microservice (6 bits) — sufficient for horizontal scaling
        builder.TryAdd(appInstanceId, 6);

        //262,144 sequences per 250ms tick (18 bits) — sufficient for high-performance scenarios
        //Per-second throughput: 262,144 × 4 = 1,048,576 IDs/s per generator
        //System-wide throughput: 8,192 generators × ~1M/s = ~8.6B IDs/s
        builder.TryAdd(timeScopedId.SequenceId, 18);

        return builder.GetValue();
    }

    /// <summary>
    /// Generates a numeric 64-bit Source-Known ID for the specified entity type using explicit partition and instance identifiers.
    /// </summary>
    /// <param name="entityType">The entity type for which IDs are generated. Must derive from <see cref="SourceKnownEntity"/>.</param>
    /// <param name="appId">Application Identifier (0..127)</param>
    /// <param name="appInstanceId">Application Instance Identifier (0..63)</param>
    public static long Generate(Type entityType, byte appId, byte appInstanceId)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        if (!typeof(SourceKnownEntity).IsAssignableFrom(entityType))
            throw new ArgumentException($"Type '{entityType.FullName}' must inherit from '{nameof(SourceKnownEntity)}'.", nameof(entityType));

        var cache = _delegateCache;
        if (!cache.FrozenDelegates.TryGetValue(entityType, out var invoker) &&
            !cache.DynamicDelegates.TryGetValue(entityType, out invoker))
        {
            invoker = cache.DynamicDelegates.GetOrAdd(entityType, static t => CreateGenerateDelegate(t));
        }

        return invoker(appId, appInstanceId);
    }

    private static Func<byte, byte, long> CreateGenerateDelegate(Type type)
    {
        var method = GenerateGenericMethodDefinition.MakeGenericMethod(type);
        return method.CreateDelegate<Func<byte, byte, long>>();
    }

    private static byte ValidateAppInstanceId(byte appInstanceId)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(appInstanceId, MaxAppInstanceId);
        return appInstanceId;
    }

    private static class EntityIdCache<TEntity> where TEntity : SourceKnownEntity
    {
        public static readonly byte DeclaredAppId = SourceKnownEntity.GetAppId<TEntity>();
    }

    private readonly byte _nexusAppInstanceId;

    public long Next<TEntity>() where TEntity : SourceKnownEntity
        => Next<TEntity>(EntityIdCache<TEntity>.DeclaredAppId, _nexusAppInstanceId);

    public long Next<TEntity>(byte appId, byte appInstanceId) where TEntity : SourceKnownEntity
        => Generate<TEntity>(appId, appInstanceId);

    public long Next(SourceKnownEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var type = entity.GetType();
        return Generate(type, SourceKnownEntity.GetAppId(type), _nexusAppInstanceId);
    }

    public long Next(Type entityType, byte appId, byte appInstanceId)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return typeof(SourceKnownEntity).IsAssignableFrom(entityType)
            ? Generate(entityType, appId, appInstanceId)
            : throw new ArgumentException($"Type '{entityType.FullName}' must inherit from '{nameof(SourceKnownEntity)}'.", nameof(entityType));
    }

    public SourceKnownId Parse(long id) => ParseId(id);

    public static SourceKnownId ParseId(long id) => ParseId(id, EpochTimeUtils.DefaultEpoch);

    internal static SourceKnownId ParseId(long id, DateTimeOffset epoch)
    {
        var parser = NumberParser.Get(id);
        var appId = (byte)parser.Read(7);
        var appInstanceId = (byte)parser.Read(6);
        var instanceId = parser.Read(18);

        var storedTimestamp = parser.ReadResidueValue();
        var fullTicks = id >= 0 ? storedTimestamp + TicksPerHalf : storedTimestamp;
        var dateTime = EpochTimeUtils.ConvertToDateTime(fullTicks, epoch);
        return new SourceKnownId(id, dateTime, instanceId, appId, appInstanceId);
    }
}
