using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using DRN.Framework.EntityFramework.Context;
using DRN.Framework.SharedKernel;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.SharedKernel.Domain.Repository;
using DRN.Framework.Utils.Entity;
using DRN.Framework.Utils.Logging;
using Microsoft.EntityFrameworkCore;

namespace DRN.Framework.EntityFramework.Domain;

/// <summary>
/// Represents an abstract repository that manages entities of type <typeparamref name="TEntity"/> within a specific database context of type <typeparamref name="TContext"/>.
/// Provides methods for CRUD operations, entity retrieval, pagination, and cancellation token management.
/// </summary>
/// <remarks>
/// Entity updates, additional filtering logic, and query includes (e.g., <c>Include</c> statements) are the responsibility of concrete subclasses.
/// </remarks>
public abstract class SourceKnownRepository<TContext, TEntity>(TContext context, IEntityUtils utils) : ISourceKnownRepository<TEntity>
    where TContext : DbContext, IDrnContext
    where TEntity : AggregateRoot
{
    private static readonly byte EntityType = SourceKnownEntity.GetEntityType<TEntity>();
    private static readonly string ChangeCountKey = $"Count.{nameof(SaveChangesAsync)}.{typeof(TEntity).Name}";
    private static readonly string GetCountKey = $"Count.{nameof(GetAsync)}.{typeof(TEntity).Name}";
    private static readonly string CreateCountKey = $"Count.{nameof(CreateAsync)}.{typeof(TEntity).Name}";
    private static readonly string DeleteCountKey = $"Count.{nameof(DeleteAsync)}.{typeof(TEntity).Name}";

    private CancellationToken _cancellationToken = CancellationToken.None;

    protected TContext Context { get; } = context;
    protected DbSet<TEntity> Entities { get; } = context.GetEntities<TEntity>();
    protected IEntityUtils Utils { get; } = utils;
    protected IScopedLog ScopedLog { get; } = utils.ScopedLog;

    /// <summary>
    /// Settings for default public members of SourceKnownRepositories
    /// </summary>
    public RepositorySettings<TEntity> Settings { get; set; } = new();

    protected IQueryable<TEntity> EntitiesWithAppliedSettings([CallerMemberName] string? caller = null)
    {
        var entities = EntitiesWithDefaultSettings(caller);
        entities = Settings.AsNoTracking ? entities.AsNoTracking() : entities;
        entities = Settings.IgnoreAutoIncludes ? entities.IgnoreAutoIncludes() : entities;

        return entities;
    }

    protected IQueryable<TEntity> EntitiesWithDefaultSettings([CallerMemberName] string? caller = null)
    {
        var repositoryType = GetType();
        var entities = Entities.TagWith(repositoryType.FullName ?? repositoryType.Name);
        if (caller != null) entities.TagWith(caller);

        if (Settings.DefaultFilter is not null)
            entities = entities.Where(Settings.DefaultFilter);

        return entities;
    }

    public CancellationToken CancellationToken
    {
        get => _cancellationToken == CancellationToken.None ? Utils.Cancellation.Token : _cancellationToken;
        set => _cancellationToken = value;
    }

    public void MergeCancellationTokens(CancellationToken other) => Utils.Cancellation.Merge(other);

    public void CancelChanges()
    {
        using var _ = ScopedLog.Measure(this);
        Utils.Cancellation.Cancel();
    }

    /// <summary>
    /// Saves all changes made in this context to the database.
    /// </summary>
    /// <returns>The number of state entries written to the database</returns>
    public async Task<int> SaveChangesAsync()
    {
        using var _ = ScopedLog.Measure(this);
        var changeCount = await Context.SaveChangesAsync(CancellationToken);
        ScopedLog.Increase(ChangeCountKey, changeCount);

        return changeCount;
    }

    public async Task<bool> AnyAsync(Expression<Func<TEntity, bool>>? predicate = null)
    {
        using var _ = ScopedLog.Measure(this);
        var any = predicate == null
            ? await EntitiesWithAppliedSettings().AnyAsync(CancellationToken)
            : await EntitiesWithAppliedSettings().AnyAsync(predicate, CancellationToken);

        return any;
    }

    public async Task<bool> AllAsync(Expression<Func<TEntity, bool>> predicate)
    {
        using var _ = ScopedLog.Measure(this);
        var any = await EntitiesWithAppliedSettings().AllAsync(predicate, CancellationToken);

        return any;
    }

    public async Task<long> CountAsync(Expression<Func<TEntity, bool>>? predicate = null)
    {
        using var _ = ScopedLog.Measure(this);
        var count = predicate == null
            ? await EntitiesWithAppliedSettings().CountAsync(CancellationToken)
            : await EntitiesWithAppliedSettings().CountAsync(predicate, CancellationToken);

        return count;
    }

    /// <summary>
    /// Finds an entity with the given source known ids
    /// </summary>
    /// <exception cref="ValidationException">Thrown when id is invalid or doesn't match the repository entity type</exception>
    public async Task<TEntity[]> GetAsync(IReadOnlyCollection<Guid> ids)
    {
        using var _ = ScopedLog.Measure(this);
        if (ids.Count == 0) return [];

        var items = await Filter(EntitiesWithAppliedSettings(), ids).ToArrayAsync(CancellationToken);
        ScopedLog.Increase(GetCountKey, items.Length);

        return items;
    }

    public async Task<TEntity[]> GetAsync(IReadOnlyCollection<SourceKnownEntityId> ids)
    {
        using var _ = ScopedLog.Measure(this);
        if (ids.Count == 0) return [];

        var items = await Filter(EntitiesWithAppliedSettings(), ids).ToArrayAsync(CancellationToken);
        ScopedLog.Increase(GetCountKey, items.Length);

        return items;
    }

    /// <summary>
    /// Finds an entity with the given source known id
    /// </summary>
    /// <exception cref="NotFoundException">Thrown when entity not found</exception>
    /// <exception cref="ValidationException">Thrown when id is invalid or doesn't match the repository entity type</exception>
    public async ValueTask<TEntity> GetAsync(Guid id) => await GetOrDefaultAsync(id)
                                                         ?? throw new NotFoundException($"{typeof(TEntity).FullName} not found: {id}");

    public async ValueTask<TEntity> GetAsync(SourceKnownEntityId id) => await GetOrDefaultAsync(id)
                                                                        ?? throw new NotFoundException($"{typeof(TEntity).FullName} not found: {id}");

    /// <summary>
    /// Finds an entity with the given source known id
    /// </summary>
    /// <exception cref="ValidationException">Thrown when id is invalid or doesn't match the repository entity type</exception>
    public async ValueTask<TEntity?> GetOrDefaultAsync(Guid id, bool validate = true)
    {
        using var _ = ScopedLog.Measure(this);
        var entityId = GetEntityId(id, validate);
        if (!entityId.Valid) return null;

        var entity = await EntitiesWithAppliedSettings().FirstOrDefaultAsync(entity => entity.Id == entityId.Source.Id, CancellationToken);

        return entity;
    }

    public async ValueTask<TEntity?> GetOrDefaultAsync(SourceKnownEntityId id, bool validate = true)
    {
        using var _ = ScopedLog.Measure(this);
        if (validate) id.Validate<TEntity>();
        if (!id.Valid) return null;

        var entity = await EntitiesWithAppliedSettings().FirstOrDefaultAsync(entity => entity.Id == id.Source.Id, CancellationToken);

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
        using var _ = ScopedLog.Measure(this);
        Add(entities);

        return await Context.SaveChangesAsync(CancellationToken);
    }

    /// <summary>
    /// equivalent to calling Remove + SaveChangesAsync
    /// </summary>
    /// <returns>The number of state entries written to the database</returns>
    public async Task<int> DeleteAsync(params IReadOnlyCollection<TEntity> entities)
    {
        using var _ = ScopedLog.Measure(this);
        Remove(entities);

        return await Context.SaveChangesAsync(CancellationToken);
    }

    /// <summary>
    /// Directly deletes entities from the database without fetching first.
    /// </summary>
    /// <returns>The total number of rows deleted in the database.</returns>
    public async Task<int> DeleteAsync(params IReadOnlyCollection<Guid> ids)
    {
        using var _ = ScopedLog.Measure(this);
        var entities = EntitiesWithDefaultSettings();

        var deletedCount = await Filter(entities.IgnoreAutoIncludes(), ids).ExecuteDeleteAsync(CancellationToken);
        ScopedLog.Increase(DeleteCountKey, deletedCount);

        return deletedCount;
    }

    public async Task<int> DeleteAsync(params IReadOnlyCollection<SourceKnownEntityId> ids)
    {
        using var _ = ScopedLog.Measure(this);
        var entities = EntitiesWithDefaultSettings();

        var deletedCount = await Filter(entities.IgnoreAutoIncludes(), ids).ExecuteDeleteAsync(CancellationToken);
        ScopedLog.Increase(DeleteCountKey, deletedCount);

        return deletedCount;
    }

    /// <exception cref="ValidationException">Thrown when id is invalid or doesn't match the repository entity type</exception>
    public SourceKnownEntityId GetEntityId(Guid id, bool validate = true)
    {
        using var _ = ScopedLog.Measure(this);
        return validate ? Utils.EntityId.Validate(id, EntityType) : Utils.EntityId.Parse(id);
    }

    public SourceKnownEntityId GetEntityId<TOtherEntity>(Guid id) where TOtherEntity : SourceKnownEntity
    {
        using var _ = ScopedLog.Measure(this);
        return Utils.EntityId.Validate(id, SourceKnownEntity.GetEntityType<TOtherEntity>());
    }

    /// <exception cref="ValidationException">Thrown when id is invalid or doesn't match the repository entity type</exception>
    public SourceKnownEntityId[] GetEntityIds(IReadOnlyCollection<Guid> ids, bool validate = true)
        => GetEntityIdsAsEnumerable(ids, validate).ToArray();

    public SourceKnownEntityId[] GetEntityIds<TOtherEntity>(IReadOnlyCollection<Guid> ids) where TOtherEntity : SourceKnownEntity
        => GetEntityIdsAsEnumerable<TOtherEntity>(ids).ToArray();

    public IEnumerable<SourceKnownEntityId> GetEntityIdsAsEnumerable(IEnumerable<Guid> ids, bool validate = true)
        => ids.Select(id => GetEntityId(id, validate));

    public IEnumerable<SourceKnownEntityId> GetEntityIdsAsEnumerable<TOtherEntity>(IEnumerable<Guid> ids) where TOtherEntity : SourceKnownEntity
        => ids.Select(GetEntityId<TOtherEntity>);

    public async Task<PaginationResultModel<TEntity>> PaginateAsync(PaginationRequest request, EntityCreatedFilter? filter = null)
        => await PaginateAsync(EntitiesWithAppliedSettings(), request, filter);

    /// <summary>
    /// Executes pagination against a specific <see cref="IQueryable{TEntity}"/>.
    /// If <paramref name="resultInfo"/> is null, starts initial pagination using the provided settings.
    /// Otherwise, continues pagination from <paramref name="resultInfo"/> and jumps to the specified page.
    /// </summary>
    /// <param name="resultInfo">
    ///     Optional. The result of a previous pagination call. If null, a new pagination session is started.
    /// </param>
    /// <param name="jumpTo">
    ///     The page number to navigate to when continuing pagination. Ignored on initial calls (<paramref name="resultInfo"/> is null).
    /// </param>
    /// <param name="pageSize">
    ///     The number of items per page for the initial pagination request.
    ///     If a positive value is not provided, <see cref="PageSize"/>'s default value (10) will be used. 
    ///     If this value is positive and differs from the page size specified in <paramref name="resultInfo" />,
    ///     the current page number will be reset to 1.
    /// </param>
    /// <param name="maxSize">
    ///     The max size value needs to be preserved when it is above the default max size
    ///     If a positive value is not provided, <see cref="PageSize"/>'s default max value(100) will be used. 
    /// </param>
    /// <param name="direction">
    ///     The sort direction for the initial pagination request. Default behavior preserve resultInfo's or use Ascending when info is null.
    ///     If this value differs from the sort direction specified in <paramref name="resultInfo" />,
    ///     the current page number will be reset to 1.
    /// </param>
    /// <param name="totalCount">Total count to be used if calculated previously</param>
    /// <param name="updateTotalCount">
    ///     Whether to calculate and return the total item count on the initial pagination request.
    /// </param>
    /// <returns>
    /// A <see cref="PaginationResultModel{TEntity}"/> containing the page of items.
    /// </returns>
    /// <remarks>
    /// When continuing pagination, jump distances beyond 10 pages in either direction are capped at 10.
    /// </remarks>
    public async Task<PaginationResultModel<TEntity>> PaginateAsync(PaginationResultInfo? resultInfo = null, long jumpTo = 1L,
        int pageSize = -1, int maxSize = -1, PageSortDirection direction = PageSortDirection.None, long totalCount = -1, bool updateTotalCount = false)
        => await PaginateAsync(EntitiesWithAppliedSettings(), resultInfo, jumpTo, pageSize, maxSize, direction, totalCount, updateTotalCount);

    public IAsyncEnumerable<PaginationResultModel<TEntity>> PaginateAllAsync(PaginationRequest request, EntityCreatedFilter? filter = null)
        => PaginateAllAsync(EntitiesWithAppliedSettings(), request, filter);

    /// <summary>
    /// Executes pagination against a specific <see cref="IQueryable{TEntity}"/>.
    /// If <paramref name="resultInfo"/> is null, starts initial pagination using the provided settings.
    /// Otherwise, continues pagination from <paramref name="resultInfo"/> and jumps to the specified page.
    /// </summary>
    /// <param name="query">
    ///     The data source to paginate.
    /// </param>
    /// <param name="resultInfo">
    ///     Optional. The result of a previous pagination call. If null, a new pagination session is started.
    /// </param>
    /// <param name="jumpTo">
    ///     The page number to navigate to when continuing pagination. Ignored on initial calls (<paramref name="resultInfo"/> is null).
    /// </param>
    /// <param name="pageSize">
    ///     The number of items per page for the initial pagination request.
    ///     If a positive value is not provided, <see cref="PageSize"/>'s default value (10) will be used. 
    ///     If this value is positive and differs from the page size specified in <paramref name="resultInfo" />,
    ///     the current page number will be reset to 1.
    /// </param>
    /// <param name="maxSize">
    ///     The max size value needs to be preserved when it is above the default max size
    ///     If a positive value is not provided, <see cref="PageSize"/>'s default max value(100) will be used. 
    /// </param>
    /// <param name="direction">
    ///     The sort direction for the initial pagination request. Default behavior preserve resultInfo's or use Ascending when info is null.
    ///     If this value differs from the sort direction specified in <paramref name="resultInfo" />,
    ///     the current page number will be reset to 1.
    /// </param>
    /// <param name="totalCount">Total count to be used if calculated previously</param>
    /// <param name="updateTotalCount">
    ///     Whether to calculate and return the total item count on the initial pagination request.
    /// </param>
    /// <returns>
    /// A <see cref="PaginationResultModel{TEntity}"/> containing the page of items.
    /// </returns>
    /// <remarks>
    /// When continuing pagination, jump distances beyond 10 pages in either direction are capped at 10.
    /// </remarks>
    protected async Task<PaginationResultModel<TEntity>> PaginateAsync(
        IQueryable<TEntity> query, PaginationResultInfo? resultInfo = null, long jumpTo = 1, int pageSize = -1, int maxSize = -1,
        PageSortDirection direction = PageSortDirection.None, long totalCount = -1, bool updateTotalCount = false)
    {
        //todo improve test cases with additional query filter
        var request = PaginationRequest.From(resultInfo, jumpTo, pageSize, maxSize, direction, totalCount, updateTotalCount);
        var result = await PaginateAsync(query, request);

        return result;
    }

    protected async Task<PaginationResultModel<TEntity>> PaginateAsync(IQueryable<TEntity> query, PaginationRequest request, EntityCreatedFilter? filter = null)
    {
        using var _ = ScopedLog.Measure(this);
        if (!request.PageCursor.IsFirstRequest)
            GetEntityId(request.GetCursorId());

        var filteredQuery = filter != null ? Filter(query, filter) : query;
        var result = await Utils.Pagination.GetResultAsync(filteredQuery, request, CancellationToken);
        ScopedLog.Increase(GetCountKey, result.ItemCount);

        return result.ToModel();
    }

    protected async IAsyncEnumerable<PaginationResultModel<TEntity>> PaginateAllAsync(
        IQueryable<TEntity> query,
        PaginationRequest request,
        EntityCreatedFilter? filter = null)
    {
        PaginationResultModel<TEntity> result;
        do
        {
            result = await PaginateAsync(query, request, filter);
            if (result.Info.HasNext)
                request = result.Info.RequestNextPage();

            yield return result;
        } while (result.Info.HasNext);
    }

    protected IQueryable<TEntity> Filter(IQueryable<TEntity> query, EntityCreatedFilter filter)
        => Utils.DateTime.Apply(query, filter);

    protected IQueryable<TEntity> Filter(IQueryable<TEntity> query, params IReadOnlyCollection<Guid> ids)
    {
        var sourceKnownIds = GetEntityIdsAsEnumerable(ids).Select(sourceKnownEntityId => sourceKnownEntityId.Source.Id).ToArray();

        return Filter(query, sourceKnownIds);
    }

    protected static IQueryable<TEntity> Filter(IQueryable<TEntity> query, params IReadOnlyCollection<SourceKnownEntityId> ids)
    {
        var sourceKnownIds = ids.Select(sourceKnownEntityId => sourceKnownEntityId.Source.Id).ToArray();

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