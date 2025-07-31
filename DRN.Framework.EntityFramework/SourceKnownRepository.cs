using DRN.Framework.EntityFramework.Context;
using DRN.Framework.SharedKernel;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.SharedKernel.Domain.Repository;
using DRN.Framework.Utils.Entity;
using DRN.Framework.Utils.Logging;
using Microsoft.EntityFrameworkCore;

namespace DRN.Framework.EntityFramework;

/// <summary>
/// Represents an abstract repository that manages entities of type <typeparamref name="TEntity"/> within a specific database context of type <typeparamref name="TContext"/>.
/// Provides methods for CRUD operations, entity retrieval, pagination, and cancellation token management.
/// </summary>
/// <remarks>
/// Entity updates, additional filtering logic, and query includes (e.g., <c>Include</c> statements) are the responsibility of concrete subclasses.
/// </remarks>
public abstract class SourceKnownRepository<TContext, TEntity> : ISourceKnownRepository<TEntity>
    where TContext : DbContext, IDrnContext
    where TEntity : AggregateRoot
{
    private static readonly byte EntityTypeId = SourceKnownEntity.GetEntityTypeId<TEntity>();
    private static readonly string ChangeCountKey = $"Count.{nameof(SaveChangesAsync)}.{typeof(TEntity).Name}";
    private static readonly string GetCountKey = $"Count.{nameof(GetAsync)}.{typeof(TEntity).Name}";
    private static readonly string CreateCountKey = $"Count.{nameof(CreateAsync)}.{typeof(TEntity).Name}";
    private static readonly string DeleteCountKey = $"Count.{nameof(DeleteAsync)}.{typeof(TEntity).Name}";

    private CancellationToken _cancellationToken = CancellationToken.None;
    private readonly ScopeName _scopeName;

    protected SourceKnownRepository(TContext context, IEntityUtils utils)
    {
        Context = context;
        Entities = context.GetEntities<TEntity>();
        Utils = utils;
        ScopedLog = utils.ScopedLog;
        _scopeName = new ScopeName(GetType().Name);
    }

    protected TContext Context { get; }
    protected DbSet<TEntity> Entities { get; }
    protected IEntityUtils Utils { get; }
    protected IScopedLog ScopedLog { get; }

    public CancellationToken CancellationToken
    {
        get => _cancellationToken == CancellationToken.None ? Utils.Cancellation.Token : _cancellationToken;
        set => _cancellationToken = value;
    }

    public void MergeCancellationTokens(CancellationToken other) => Utils.Cancellation.Merge(other);

    public void CancelChanges()
    {
        using var _ = ScopedLog.Measure(_scopeName.GetKey());
        Utils.Cancellation.Cancel();
    }

    /// <summary>
    /// Saves all changes made in this context to the database.
    /// </summary>
    /// <returns>The number of state entries written to the database</returns>
    public async Task<int> SaveChangesAsync()
    {
        using var _ = ScopedLog.Measure(_scopeName.GetKey());
        var changeCount = await Context.SaveChangesAsync(CancellationToken);
        ScopedLog.Increase(ChangeCountKey, changeCount);

        return changeCount;
    }

    /// <summary>
    /// Finds an entity with the given source known ids
    /// </summary>
    /// <exception cref="ValidationException">Thrown when id is invalid or doesn't match the repository entity type</exception>
    public async Task<TEntity[]> GetAsync(IReadOnlyCollection<Guid> ids, bool ignoreAutoIncludes = false)
    {
        using var _ = ScopedLog.Measure(_scopeName.GetKey());
        var items = ignoreAutoIncludes
            ? await Filter(Entities, ids).IgnoreAutoIncludes().ToArrayAsync(CancellationToken)
            : await Filter(Entities, ids).ToArrayAsync(CancellationToken);
        ScopedLog.Increase(GetCountKey, items.Length);

        return items;
    }

    /// <summary>
    /// Finds an entity with the given source known id
    /// </summary>
    /// <exception cref="NotFoundException">Thrown when entity not found</exception>
    /// <exception cref="ValidationException">Thrown when id is invalid or doesn't match the repository entity type</exception>
    public async ValueTask<TEntity> GetAsync(Guid id, bool ignoreAutoIncludes = false) =>
        await GetOrDefaultAsync(id, ignoreAutoIncludes: ignoreAutoIncludes)
        ?? throw new NotFoundException($"{typeof(TEntity).FullName} not found: {id}");

    /// <summary>
    /// Finds an entity with the given source known id
    /// </summary>
    /// <exception cref="ValidationException">Thrown when id is invalid or doesn't match the repository entity type</exception>
    public async ValueTask<TEntity?> GetOrDefaultAsync(Guid id, bool throwException = true, bool ignoreAutoIncludes = false)
    {
        using var _ = ScopedLog.Measure(_scopeName.GetKey());
        var entityId = ValidateEntityId(id, throwException);
        if (!entityId.Valid)
            return null;

        var entity = ignoreAutoIncludes
            ? await Entities.IgnoreAutoIncludes().FirstOrDefaultAsync(entity => entity.Id == entityId.Source.Id, CancellationToken)
            : await Entities.FindAsync(entityId.Source.Id, CancellationToken);

        return entity;
    }


    /// <summary>
    /// Begins tracking the given entities, and any other reachable entities that are not already being tracked,
    /// in the Added state such that they will be inserted into the database when SaveChanges() is called.
    /// </summary>
    public void Add(params IReadOnlyCollection<TEntity> entities)
    {
        Entities.AddRange(entities);
        ScopedLog.Increase(CreateCountKey, entities.Count);
    }

    /// <summary>
    /// Begins tracking the given entities in the Deleted state such that they will be removed from the database when SaveChanges() is called.
    /// </summary>
    public void Remove(params IReadOnlyCollection<TEntity> entities)
    {
        Entities.RemoveRange(entities);
        ScopedLog.Increase(DeleteCountKey, entities.Count);
    }


    /// <summary>
    /// equivalent to calling Add + SaveChangesAsync
    /// </summary>
    /// <returns>The number of state entries written to the database</returns>
    public async Task<int> CreateAsync(params IReadOnlyCollection<TEntity> entities)
    {
        using var _ = ScopedLog.Measure(_scopeName.GetKey());
        Add(entities);

        return await Context.SaveChangesAsync(CancellationToken);
    }

    /// <summary>
    /// equivalent to calling Remove + SaveChangesAsync
    /// </summary>
    /// <returns>The number of state entries written to the database</returns>
    public async Task<int> DeleteAsync(params IReadOnlyCollection<TEntity> entities)
    {
        using var _ = ScopedLog.Measure(_scopeName.GetKey());
        Remove(entities);

        return await Context.SaveChangesAsync(CancellationToken);
    }

    /// <summary>
    /// Directly deletes entities from the database without fetching first.
    /// </summary>
    /// <returns>The total number of rows deleted in the database.</returns>
    public async Task<int> DeleteAsync(params IReadOnlyCollection<Guid> ids)
    {
        using var _ = ScopedLog.Measure(_scopeName.GetKey());
        var deletedCount = await Filter(Entities, ids).ExecuteDeleteAsync(CancellationToken);
        ScopedLog.Increase(DeleteCountKey, deletedCount);

        return deletedCount;
    }

    //todo evaluate validation scope log support
    /// <exception cref="ValidationException">Thrown when id is invalid or doesn't match the repository entity type</exception>
    public SourceKnownEntityId ValidateEntityId(Guid id, bool throwException = true)
    {
        using var _ = ScopedLog.Measure(_scopeName.GetKey());
        return throwException ? Utils.EntityId.Validate(id, EntityTypeId) : Utils.EntityId.Parse(id);
    }

    /// <exception cref="ValidationException">Thrown when id is invalid or doesn't match the repository entity type</exception>
    public SourceKnownEntityId[] ValidateEntityIds(IReadOnlyCollection<Guid> ids, bool throwException = true)
        => ValidateEntityIdsAsEnumerable(ids, throwException).ToArray();

    public IEnumerable<SourceKnownEntityId> ValidateEntityIdsAsEnumerable(IEnumerable<Guid> ids, bool throwException = true)
        => ids.Select(id => ValidateEntityId(id, throwException));


    public async Task<PaginationResult<TEntity>> PaginateAsync(PaginationRequest request, EntityCreatedFilter? filter = null, bool ignoreAutoIncludes = false)
        => await PaginateAsync(Entities, request, filter, ignoreAutoIncludes);

    public IAsyncEnumerable<PaginationResult<TEntity>> PaginateAllAsync(PaginationRequest request, EntityCreatedFilter? filter = null, bool ignoreAutoIncludes = false)
        => PaginateAllAsync(Entities, request, filter, ignoreAutoIncludes);

    protected async Task<PaginationResult<TEntity>> PaginateAsync(IQueryable<TEntity> query, PaginationRequest request, EntityCreatedFilter? filter = null,
        bool ignoreAutoIncludes = false)
    {
        using var _ = ScopedLog.Measure(_scopeName.GetKey());
        if (!request.PageCursor.IsFirstRequest)
            ValidateEntityId(request.GetCursorId());

        var filteredQuery = filter != null ? Filter(Entities, filter) : query;
        filteredQuery = ignoreAutoIncludes ? filteredQuery.IgnoreAutoIncludes() : filteredQuery;

        var result = await Utils.Pagination.GetResultAsync(filteredQuery, request, CancellationToken);
        ScopedLog.Increase(GetCountKey, result.ItemCount);

        return result;
    }

    protected async IAsyncEnumerable<PaginationResult<TEntity>> PaginateAllAsync(
        IQueryable<TEntity> query,
        PaginationRequest request,
        EntityCreatedFilter? filter = null,
        bool ignoreAutoIncludes = false)
    {
        PaginationResult<TEntity> result;
        do
        {
            result = await PaginateAsync(query, request, filter, ignoreAutoIncludes);
            if (result.HasNext)
                request = result.RequestNextPage();

            yield return result;
        } while (result.HasNext);
    }


    protected IQueryable<TEntity> Filter(IQueryable<TEntity> query, EntityCreatedFilter filter)
        => Utils.DateTime.Apply(query, filter);

    protected IQueryable<TEntity> Filter(IQueryable<TEntity> query, params IReadOnlyCollection<Guid> ids)
    {
        var sourceKnownIds = ValidateEntityIdsAsEnumerable(ids).Select(sourceKnownEntityId => sourceKnownEntityId.Source.Id).ToArray();

        return Filter(query, sourceKnownIds);
    }

    protected static IQueryable<TEntity> Filter(IQueryable<TEntity> query, params IReadOnlyCollection<long> ids)
    {
        if (ids.Count == 0)
            throw new ValidationException("Id count must be greater than 0");
        if (ids.Count != 1)
            return query.Where(entity => ids.Contains(entity.Id));

        var id = ids.First();
        return query.Where(entity => id == entity.Id);
    }
}