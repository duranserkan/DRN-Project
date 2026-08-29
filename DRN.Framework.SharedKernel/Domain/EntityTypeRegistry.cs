using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace DRN.Framework.SharedKernel.Domain;

/// <summary>
/// Centralized immutable registry for domain entity type metadata and partition mappings.
/// Provides high-performance O(1) FrozenDictionary lookups for startup-registered entities
/// while supporting thread-safe dynamic fallback for unit tests and ad-hoc types.
/// </summary>
/// <remarks>
/// <para><b>Process-Level Metadata Truth:</b></para>
/// Entity metadata declared via <see cref="EntityTypeAttribute"/> constitutes pure compile-time domain truth
/// (<see cref="Type"/> to <see cref="EntityTypeId"/>). This mapping is globally invariant across all
/// <c>DbContext</c> instances, dependency injection containers, and hosted services within the process.
/// The registry contains no scoped or ambient state.
/// <para><b>Concurrency &amp; Thread-Safety:</b></para>
/// <list type="bullet">
/// <item><b>Lock-Free Reads:</b> Steady-state lookups (<see cref="GetEntityTypeId(Type)"/> and <see cref="GetEntityType(EntityTypeId)"/>)
/// read from an immutable <see cref="FrozenDictionary{TKey, TValue}"/> snapshot via volatile pointer reads without locks or memory barriers.</item>
/// <item><b>Atomic Snapshot Swapping:</b> Multi-threaded startup registrations (e.g. concurrent integration tests or multi-context hosts)
/// are synchronized via a mutex lock. Mutations build a unified <see cref="FrozenDictionary{TKey, TValue}"/> and atomically swap the active snapshot.</item>
/// <item><b>Idempotency:</b> Subsequent registration calls containing previously registered types exit immediately as a 0-allocation no-op.</item>
/// <item><b>Dynamic Fallback:</b> Unmapped types (such as dynamic mock entities in isolated unit tests) are resolved and cached thread-safely.</item>
/// </list>
/// </remarks>
[SuppressMessage("ReSharper", "InconsistentlySynchronizedField")]
public static class EntityTypeRegistry
{
    private sealed class RegistrySnapshot(
        FrozenDictionary<Type, EntityTypeId> typeToId,
        FrozenDictionary<EntityTypeId, Type> idToType,
        ConcurrentDictionary<Type, EntityTypeId> dynamicTypeToId,
        ConcurrentDictionary<EntityTypeId, Type> dynamicIdToType)
    {
        public readonly FrozenDictionary<Type, EntityTypeId> TypeToId = typeToId;
        public readonly FrozenDictionary<EntityTypeId, Type> IdToType = idToType;
        public readonly ConcurrentDictionary<Type, EntityTypeId> DynamicTypeToId = dynamicTypeToId;
        public readonly ConcurrentDictionary<EntityTypeId, Type> DynamicIdToType = dynamicIdToType;
    }

    private static readonly Lock SyncLock = new();

    private static volatile RegistrySnapshot _snapshot = new(
        FrozenDictionary<Type, EntityTypeId>.Empty,
        FrozenDictionary<EntityTypeId, Type>.Empty,
        new ConcurrentDictionary<Type, EntityTypeId>(),
        new ConcurrentDictionary<EntityTypeId, Type>());

    /// <summary>
    /// Bulk registers and freezes domain entity types discovered at startup.
    /// Called during startup validation (e.g. by DrnContextServiceRegistrationAttribute).
    /// </summary>
    public static void Register(IEnumerable<Type> entityTypes)
    {
        ArgumentNullException.ThrowIfNull(entityTypes);

        lock (SyncLock)
        {
            var current = _snapshot;
            var typeToIdMap = new Dictionary<Type, EntityTypeId>(current.TypeToId);
            var idToTypeMap = new Dictionary<EntityTypeId, Type>(current.IdToType);

            var anyNew = false;
            // Merge any previously recorded dynamic entries
            foreach (var (k, v) in current.DynamicTypeToId) { typeToIdMap[k] = v; anyNew = true; }
            foreach (var (k, v) in current.DynamicIdToType) { idToTypeMap[k] = v; anyNew = true; }

            foreach (var type in entityTypes)
            {
                if (typeToIdMap.ContainsKey(type)) continue;
                anyNew = true;

                var attribute = type.GetCustomAttribute<EntityTypeAttribute>()
                    ?? throw new InvalidOperationException($"{type.Name} must use {nameof(EntityTypeAttribute)}");

                ArgumentOutOfRangeException.ThrowIfGreaterThan(attribute.AppId, IAppId.MaxAppId);
                var entityTypeId = new EntityTypeId(attribute.EntityType, attribute.AppId);

                if (idToTypeMap.TryGetValue(entityTypeId, out var existingType) && existingType != type)
                {
                    throw new InvalidOperationException(
                        $"Entity type value: {entityTypeId.EntityType} with AppId: {entityTypeId.AppId} " +
                        $"is used by both {existingType.FullName} and {type.FullName}");
                }

                typeToIdMap[type] = entityTypeId;
                idToTypeMap[entityTypeId] = type;
            }

            if (!anyNew && current.TypeToId.Count > 0)
                return;

            // Atomically replace with a new Frozen snapshot
            _snapshot = new RegistrySnapshot(
                typeToIdMap.ToFrozenDictionary(),
                idToTypeMap.ToFrozenDictionary(),
                new ConcurrentDictionary<Type, EntityTypeId>(),
                new ConcurrentDictionary<EntityTypeId, Type>());
        }
    }

    /// <summary>
    /// Retrieves EntityTypeId for a Type. Uses FrozenDictionary with dynamic fallback.
    /// </summary>
    public static EntityTypeId GetEntityTypeId(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        // ReSharper disable once InconsistentlySynchronizedField
        var snapshot = _snapshot;
        return snapshot.TypeToId.TryGetValue(entityType, out var entityTypeId) || snapshot.DynamicTypeToId.TryGetValue(entityType, out entityTypeId)
            ? entityTypeId
            : ResolveAndRegisterDynamic(entityType);
    }

    /// <summary>
    /// Retrieves Type for an EntityTypeId. Uses FrozenDictionary with dynamic fallback.
    /// </summary>
    public static Type? GetEntityType(EntityTypeId key)
    {
        var snapshot = _snapshot;
        return snapshot.IdToType.TryGetValue(key, out var type)
            ? type
            : snapshot.DynamicIdToType.GetValueOrDefault(key);
    }

    private static EntityTypeId ResolveAndRegisterDynamic(Type type)
    {
        lock (SyncLock)
        {
            var snapshot = _snapshot;
            if (snapshot.TypeToId.TryGetValue(type, out var entityTypeId) || snapshot.DynamicTypeToId.TryGetValue(type, out entityTypeId))
                return entityTypeId;

            var attribute = type.GetCustomAttribute<EntityTypeAttribute>()
                            ?? throw new InvalidOperationException($"{type.Name} must use {nameof(EntityTypeAttribute)}");

            ArgumentOutOfRangeException.ThrowIfGreaterThan(attribute.AppId, IAppId.MaxAppId);
            entityTypeId = new EntityTypeId(attribute.EntityType, attribute.AppId);

            if (snapshot.IdToType.TryGetValue(entityTypeId, out var existingType) && existingType != type)
                throw new InvalidOperationException(
                    $"Entity type value: {entityTypeId.EntityType} with AppId: {entityTypeId.AppId} " +
                    $"is used by both {existingType.FullName} and {type.FullName}");

            if (snapshot.DynamicIdToType.TryGetValue(entityTypeId, out var dynamicType) && dynamicType != type)
                throw new InvalidOperationException(
                    $"Entity type value: {entityTypeId.EntityType} with AppId: {entityTypeId.AppId} " +
                    $"is used by both {dynamicType.FullName} and {type.FullName}");

            snapshot.DynamicIdToType[entityTypeId] = type;
            snapshot.DynamicTypeToId[type] = entityTypeId;

            return entityTypeId;
        }
    }
}
