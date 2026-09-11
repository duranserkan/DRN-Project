using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Extensions;
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

    /// <summary>
    /// Generates Ids for the specified entity.
    /// Resolves appId from the entity's [EntityType] attribute, and appInstanceId from appsettings.
    /// Uses <see cref="IEpochTimeUtils.Epoch"/>
    /// </summary>
    /// <param name="entity">The entity for which Ids are generated. Must derive from <see cref="SourceKnownEntity"/>.</param>
    long Next(SourceKnownEntity entity);

    /// <summary>
    /// Generates Ids for the specified entity type using its declared AppId and the configured AppInstanceId.
    /// </summary>
    /// <param name="entityType">The entity type for which Ids are generated. Must derive from <see cref="SourceKnownEntity"/>.</param>
    long Next(Type entityType);

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

    private const int AppInstanceShift = 18;
    private const int AppIdShift = AppInstanceShift + 6;
    private const int TimestampShift = AppIdShift + 7;
    private const uint SequenceMask = (1U << AppInstanceShift) - 1;

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
        .GetMethods(BindingFlag.StaticNonPublic)
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
    internal static long Generate<TEntity>(byte appId, byte appInstanceId) where TEntity : SourceKnownEntity
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(appId, MaxAppId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(appInstanceId, MaxAppInstanceId);

        // Retains time validation and thread-safe sequence allocation.
        var timeScopedId = SequenceManager<TEntity>.GetTimeScopedId();
        var storedTimestamp = timeScopedId.TimeStamp & uint.MaxValue;

        var value = (storedTimestamp << TimestampShift)
                    | ((long)appId << AppIdShift)
                    | ((long)appInstanceId << AppInstanceShift)
                    | (timeScopedId.SequenceId & SequenceMask);

        // First half is negative; second half is nonnegative.
        return timeScopedId.TimeStamp < TicksPerHalf
            ? value | long.MinValue
            : value;
    }

    /// <summary>
    /// Generates a numeric 64-bit Source-Known ID for the specified entity type using explicit partition and instance identifiers.
    /// </summary>
    /// <param name="entityType">The entity type for which IDs are generated. Must derive from <see cref="SourceKnownEntity"/>.</param>
    /// <param name="appId">Application Identifier (0..127)</param>
    /// <param name="appInstanceId">Application Instance Identifier (0..63)</param>
    internal static long Generate(Type entityType, byte appId, byte appInstanceId)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        if (!typeof(SourceKnownEntity).IsAssignableFrom(entityType))
            throw new ArgumentException($"Type '{entityType.FullName}' must inherit from '{nameof(SourceKnownEntity)}'.", nameof(entityType));

        return GenerateValidated(entityType, appId, appInstanceId);
    }

    private static long GenerateValidated(Type entityType, byte appId, byte appInstanceId)
    {
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

    private readonly byte _nexusAppInstanceId;

    public long Next<TEntity>() where TEntity : SourceKnownEntity
        => Generate<TEntity>(SourceKnownEntity.GetAppId<TEntity>(), _nexusAppInstanceId);

    public long Next(SourceKnownEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return Next(entity.GetType());
    }

    public long Next(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return typeof(SourceKnownEntity).IsAssignableFrom(entityType)
            ? GenerateValidated(entityType, SourceKnownEntity.GetAppId(entityType), _nexusAppInstanceId)
            : throw new ArgumentException($"Type '{entityType.FullName}' must inherit from '{nameof(SourceKnownEntity)}'.", nameof(entityType));
    }

    public SourceKnownId Parse(long id) => ParseId(id);

    public static SourceKnownId ParseId(long id) => ParseId(id, EpochTimeUtils.DefaultEpoch);

    internal static SourceKnownId ParseId(long id, DateTimeOffset epoch)
    {
        var appId = (byte)((id >> AppIdShift) & MaxAppId);
        var appInstanceId = (byte)((id >> AppInstanceShift) & MaxAppInstanceId);
        var instanceId = (uint)(id & SequenceMask);

        // Mask removes sign extension and the epoch-half sign bit.
        var storedTimestamp = (id >> TimestampShift) & uint.MaxValue;
        var fullTicks = id >= 0 ? storedTimestamp + TicksPerHalf : storedTimestamp;
        var dateTime = EpochTimeUtils.ConvertToDateTime(fullTicks, epoch);
        return new SourceKnownId(id, dateTime, instanceId, appId, appInstanceId);
    }
}
