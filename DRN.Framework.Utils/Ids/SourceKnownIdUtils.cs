using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Numbers;
using DRN.Framework.Utils.Settings;
using DRN.Framework.Utils.Time;

namespace DRN.Framework.Utils.Ids;

public interface ISourceKnownIdUtils
{
    /// <summary>
    /// Generates Ids for the app, app instance and entity.
    /// Gets appId and appInstanceId from appsettings.
    /// Uses <see cref="IEpochTimeUtils.Epoch"/>"
    /// </summary>
    /// <typeparam name="TEntity">The entity type for which Ids are generated. Must be a reference type.</typeparam>
    long Next<TEntity>() where TEntity : class;

    long Next<TEntity>(byte appId, byte appInstanceId, DateTimeOffset? epoch = null) where TEntity : class;

    /// <summary>
    /// Generates Ids for the specified entity type.
    /// Gets appId and appInstanceId from appsettings.
    /// Uses <see cref="IEpochTimeUtils.Epoch"/>"
    /// </summary>
    /// <param name="entityType">The entity type for which Ids are generated. Must be a reference type (class).</param>
    long Next(Type entityType);

    /// <summary>
    /// Generates Ids for the specified entity type using the provided appId, appInstanceId, and epoch.
    /// </summary>
    /// <param name="entityType">The entity type for which Ids are generated. Must be a reference type (class).</param>
    /// <param name="appId">Application Identifier (0..127)</param>
    /// <param name="appInstanceId">Application Instance Identifier (0..63)</param>
    /// <param name="epoch">Custom epoch if overriding default</param>
    long Next(Type entityType, byte appId, byte appInstanceId, DateTimeOffset? epoch = null);

    /// <summary>
    /// Pre-compiles and warms up ID generation expression delegates for the specified entity types.
    /// Eliminates cold-start JIT and expression compilation overhead during application startup.
    /// </summary>
    /// <param name="entityTypes">Collection of entity class types to warm up.</param>
    void Warmup(IEnumerable<Type> entityTypes) => SourceKnownIdUtils.Warmup(entityTypes);

    SourceKnownId Parse(long id, DateTimeOffset? epoch = null);
}

[Singleton<ISourceKnownIdUtils>]
public class SourceKnownIdUtils(IAppSettings appSettings, IEpochTimeUtils epochTimeUtils) : ISourceKnownIdUtils
{
    public const byte MaxAppId = IAppId.MaxAppId;
    public const byte MaxAppInstanceId = 63;
    public const long TicksPerHalf = 1L << 32;        // 2^32 ticks per half-epoch
    public const long MaxEpochTicks = (TicksPerHalf << 1) - 1; // 2^33 - 1: full ~68-year epoch (both halves)

    private static readonly ConcurrentDictionary<Type, Func<SourceKnownIdUtils, long>> TypeNextDelegateCache = new();
    private static readonly ConcurrentDictionary<Type, Func<SourceKnownIdUtils, byte, byte, DateTimeOffset?, long>> TypeNextExplicitDelegateCache = new();
    private static readonly ConcurrentDictionary<Type, Func<byte, byte, DateTimeOffset?, long>> TypeGenerateDelegateCache = new();

    /// <summary>
    /// Pre-compiles and warms up ID generation expression delegates for the specified entity types.
    /// Eliminates cold-start JIT and expression compilation overhead during application startup.
    /// </summary>
    /// <param name="entityTypes">Collection of entity class types to warm up.</param>
    public static void Warmup(IEnumerable<Type> entityTypes)
    {
        ArgumentNullException.ThrowIfNull(entityTypes);

        foreach (var type in entityTypes)
        {
            if (type is null || !type.IsClass) continue;

            TypeNextDelegateCache.GetOrAdd(type, static t => CompileNextDelegate(t));
            TypeNextExplicitDelegateCache.GetOrAdd(type, static t => CompileNextExplicitDelegate(t));
            TypeGenerateDelegateCache.GetOrAdd(type, static t => CompileGenerateDelegate(t));
        }
    }

    public static long Generate<TEntity>(byte appId, byte appInstanceId, DateTimeOffset? epoch = null) where TEntity : class
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(appId, MaxAppId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(appInstanceId, MaxAppInstanceId);

        var targetEpoch = epoch ?? EpochTimeUtils.DefaultEpoch;
        var builder = NumberBuilder.GetLong();
        var timeScopedId = SequenceManager<TEntity>.GetTimeScopedId(targetEpoch);

        //Timestamp with 250ms precision (4 ticks per second)
        //Sub-second ordering eliminates coarse-grained temporal ambiguity while preserving throughput.
        if (timeScopedId.TimeStamp is < 0 or > MaxEpochTicks)
            throw new InvalidOperationException($"Timestamp: {timeScopedId.TimeStamp} must be between 0 and {MaxEpochTicks}");

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

    public static long Generate(Type entityType, byte appId, byte appInstanceId, DateTimeOffset? epoch = null)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        if (!entityType.IsClass)
            throw new ArgumentException($"Type '{entityType.FullName}' must be a reference type (class).", nameof(entityType));

        var invoker = TypeGenerateDelegateCache.GetOrAdd(entityType, static type => CompileGenerateDelegate(type));

        return invoker(appId, appInstanceId, epoch);
    }

    private static Func<byte, byte, DateTimeOffset?, long> CompileGenerateDelegate(Type type)
    {
        var method = typeof(SourceKnownIdUtils)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m is { Name: nameof(Generate), IsGenericMethodDefinition: true } && m.GetParameters().Length == 3)
            .MakeGenericMethod(type);

        var appIdParam = Expression.Parameter(typeof(byte), "appId");
        var appInstanceIdParam = Expression.Parameter(typeof(byte), "appInstanceId");
        var epochParam = Expression.Parameter(typeof(DateTimeOffset?), "epoch");

        var call = Expression.Call(method, appIdParam, appInstanceIdParam, epochParam);
        return Expression
            .Lambda<Func<byte, byte, DateTimeOffset?, long>>(call, appIdParam, appInstanceIdParam, epochParam)
            .Compile();
    }

    private static byte ValidateAppId(byte appId)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(appId, MaxAppId);
        return appId;
    }

    private static byte ValidateAppInstanceId(byte appInstanceId)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(appInstanceId, MaxAppInstanceId);
        return appInstanceId;
    }

    private static class EntityIdCache<TEntity> where TEntity : class
    {
        public static readonly bool IsSourceKnownEntity = typeof(SourceKnownEntity).IsAssignableFrom(typeof(TEntity));
        public static readonly byte? DeclaredAppId = IsSourceKnownEntity
            ? SourceKnownEntity.GetAppId(typeof(TEntity))
            : null;
    }

    private readonly byte _nexusAppId = ValidateAppId(appSettings.NexusAppSettings.AppId);
    private readonly byte _nexusAppInstanceId = ValidateAppInstanceId(appSettings.NexusAppSettings.AppInstanceId);
    private readonly DateTimeOffset _epoch = epochTimeUtils.Epoch;

    public long Next<TEntity>() where TEntity : class
    {
        var appId = EntityIdCache<TEntity>.DeclaredAppId ?? _nexusAppId;
        return Next<TEntity>(appId, _nexusAppInstanceId, _epoch);
    }

    public long Next<TEntity>(byte appId, byte appInstanceId, DateTimeOffset? epoch = null) where TEntity : class
        => Generate<TEntity>(appId, appInstanceId, epoch ?? _epoch);

    public long Next(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        if (!entityType.IsClass)
            throw new ArgumentException($"Type '{entityType.FullName}' must be a reference type (class).", nameof(entityType));

        var invoker = TypeNextDelegateCache.GetOrAdd(entityType, static type => CompileNextDelegate(type));

        return invoker(this);
    }

    private static Func<SourceKnownIdUtils, long> CompileNextDelegate(Type type)
    {
        var method = typeof(SourceKnownIdUtils)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m is { Name: nameof(Next), IsGenericMethodDefinition: true } && m.GetParameters().Length == 0)
            .MakeGenericMethod(type);

        var instanceParam = Expression.Parameter(typeof(SourceKnownIdUtils), "utils");
        var call = Expression.Call(instanceParam, method);
        return Expression.Lambda<Func<SourceKnownIdUtils, long>>(call, instanceParam).Compile();
    }

    public long Next(Type entityType, byte appId, byte appInstanceId, DateTimeOffset? epoch = null)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        if (!entityType.IsClass)
            throw new ArgumentException($"Type '{entityType.FullName}' must be a reference type (class).", nameof(entityType));

        var invoker = TypeNextExplicitDelegateCache.GetOrAdd(entityType, static type => CompileNextExplicitDelegate(type));

        return invoker(this, appId, appInstanceId, epoch);
    }

    private static Func<SourceKnownIdUtils, byte, byte, DateTimeOffset?, long> CompileNextExplicitDelegate(Type type)
    {
        var method = typeof(SourceKnownIdUtils)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m is { Name: nameof(Next), IsGenericMethodDefinition: true } && m.GetParameters().Length == 3)
            .MakeGenericMethod(type);

        var instanceParam = Expression.Parameter(typeof(SourceKnownIdUtils), "utils");
        var appIdParam = Expression.Parameter(typeof(byte), "appId");
        var appInstanceIdParam = Expression.Parameter(typeof(byte), "appInstanceId");
        var epochParam = Expression.Parameter(typeof(DateTimeOffset?), "epoch");

        var call = Expression.Call(instanceParam, method, appIdParam, appInstanceIdParam, epochParam);
        return Expression.Lambda<Func<SourceKnownIdUtils, byte, byte, DateTimeOffset?, long>>(
            call, instanceParam, appIdParam, appInstanceIdParam, epochParam).Compile();
    }

    public SourceKnownId Parse(long id, DateTimeOffset? epoch = null) => ParseId(id, epoch ?? _epoch);

    public static SourceKnownId ParseId(long id, DateTimeOffset epoch)
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
