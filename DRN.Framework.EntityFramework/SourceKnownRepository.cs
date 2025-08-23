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
public abstract class SourceKnownRepository<TContext, TEntity>(TContext context, IEntityUtils utils) : ISourceKnownRepository<TEntity>
    where TContext : DbContext, IDrnContext
    where TEntity : AggregateRoot
{
    private static readonly byte EntityTypeId = SourceKnownEntity.GetEntityTypeId<TEntity>();
    private static readonly string ChangeCountKey = $"Count.{nameof(SaveChangesAsync)}.{typeof(TEntity).Name}";
    private static readonly string GetCountKey = $"Count.{nameof(GetAsync)}.{typeof(TEntity).Name}";
    private static readonly string CreateCountKey = $"Count.{nameof(CreateAsync)}.{typeof(TEntity).Name}";
    private static readonly string DeleteCountKey = $"Count.{nameof(DeleteAsync)}.{typeof(TEntity).Name}";

    private CancellationToken _cancellationToken = CancellationToken.None;

    protected TContext Context { get; } = context;
    protected DbSet<TEntity> Entities { get; } = context.GetEntities<TEntity>();

    protected IQueryable<TEntity> EntitiesWithAppliedSettings
    {
        get
        {
            IQueryable<TEntity> entities = Entities;

            entities = Settings.AsNoTracking ? entities.AsNoTracking() : entities;
            entities = Settings.IgnoreAutoIncludes ? Entities.IgnoreAutoIncludes() : entities;

            return entities;
        }
    }

    protected IEntityUtils Utils { get; } = utils;
    protected IScopedLog ScopedLog { get; } = utils.ScopedLog;

    /// <summary>
    /// Settings for default public members of SourceKnownRepositories
    /// </summary>
    public RepositorySettings Settings { get; set; } = new();

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

    public async Task<bool> AnyAsync()
    {
        using var _ = ScopedLog.Measure(this);
        var any = await Entities.AnyAsync(CancellationToken);
        
        return any;
    }

    /// <summary>
    /// Finds an entity with the given source known ids
    /// </summary>
    /// <exception cref="ValidationException">Thrown when id is invalid or doesn't match the repository entity type</exception>
    public async Task<TEntity[]> GetAsync(IReadOnlyCollection<Guid> ids)
    {
        using var _ = ScopedLog.Measure(this);
        var items = await Filter(EntitiesWithAppliedSettings, ids).ToArrayAsync(CancellationToken);
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
    /// <summary>
    /// Finds an entity with the given source known id
    /// </summary>
    /// <exception cref="ValidationException">Thrown when id is invalid or doesn't match the repository entity type</exception>
    public async ValueTask<TEntity?> GetOrDefaultAsync(Guid id, bool throwException = true)
    {
        using var _ = ScopedLog.Measure(this);
        var entityId = ValidateEntityId(id, throwException);
        if (!entityId.Valid)
            return null;

        var entity = await EntitiesWithAppliedSettings.FirstOrDefaultAsync(entity => entity.Id == entityId.Source.Id, CancellationToken);

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
        var deletedCount = await Filter(Entities, ids).ExecuteDeleteAsync(CancellationToken);
        ScopedLog.Increase(DeleteCountKey, deletedCount);

        return deletedCount;
    }

    //todo evaluate validation scope log support
    /// <exception cref="ValidationException">Thrown when id is invalid or doesn't match the repository entity type</exception>
    public SourceKnownEntityId ValidateEntityId(Guid id, bool throwException = true)
    {
        using var _ = ScopedLog.Measure(this);
        return throwException ? Utils.EntityId.Validate(id, EntityTypeId) : Utils.EntityId.Parse(id);
    }

    /// <exception cref="ValidationException">Thrown when id is invalid or doesn't match the repository entity type</exception>
    public SourceKnownEntityId[] ValidateEntityIds(IReadOnlyCollection<Guid> ids, bool throwException = true)
        => ValidateEntityIdsAsEnumerable(ids, throwException).ToArray();

    public IEnumerable<SourceKnownEntityId> ValidateEntityIdsAsEnumerable(IEnumerable<Guid> ids, bool throwException = true)
        => ids.Select(id => ValidateEntityId(id, throwException));


    public async Task<PaginationResultModel<TEntity>> PaginateAsync(PaginationRequest request, EntityCreatedFilter? filter = null)
        => await PaginateAsync(EntitiesWithAppliedSettings, request, filter);

    //todo test
    /// <summary>
    /// Begins or continues pagination on the entity set.
    /// When called without <paramref name="resultInfo"/>, starts initial pagination using the provided settings.
    /// Otherwise, continues from the given <paramref name="resultInfo"/> and jumps to the specified page.
    /// </summary>
    /// <param name="resultInfo">
    /// Optional. The result of a previous pagination call. If null, a new pagination session is started.
    /// </param>
    /// <param name="jumpTo">
    /// The page number to navigate to when continuing pagination. Ignored on initial calls (<paramref name="resultInfo"/> is null).
    /// </param>
    /// <param name="pageSize">
    /// The number of items per page for the initial pagination request. Ignored when <paramref name="resultInfo"/> is provided.
    /// </param>
    /// <param name="updateTotalCount">
    /// Whether to calculate and return the total item count on the initial pagination request.
    /// </param>
    /// <param name="direction">
    /// The sort direction for the initial pagination request. Ignored when <paramref name="resultInfo"/> is provided.
    /// </param>
    /// <returns>
    /// A <see cref="PaginationResultModel{TEntity}"/> containing the page of items.
    /// </returns>
    /// <remarks>
    /// When continuing pagination, jump distances beyond 10 pages in either direction are capped at 10.
    /// </remarks>
    public async Task<PaginationResultModel<TEntity>> PaginateAsync(PaginationResultInfo? resultInfo = null,
        long jumpTo = 1, int pageSize = PageSize.SizeDefault, bool updateTotalCount = false, PageSortDirection direction = PageSortDirection.Ascending)
        => await PaginateAsync(EntitiesWithAppliedSettings, resultInfo, jumpTo, pageSize, updateTotalCount, direction);

    public IAsyncEnumerable<PaginationResultModel<TEntity>> PaginateAllAsync(PaginationRequest request, EntityCreatedFilter? filter = null)
        => PaginateAllAsync(EntitiesWithAppliedSettings, request, filter);

    /// <summary>
    /// Executes pagination against a specific <see cref="IQueryable{TEntity}"/>.
    /// If <paramref name="resultInfo"/> is null, starts initial pagination using the provided settings.
    /// Otherwise, continues pagination from <paramref name="resultInfo"/> and jumps to the specified page.
    /// </summary>
    /// <param name="query">
    /// The data source to paginate.
    /// </param>
    /// <param name="resultInfo">
    /// Optional. The result of a previous pagination call. If null, a new pagination session is started.
    /// </param>
    /// <param name="jumpTo">
    /// The page number to navigate to when continuing pagination. Ignored on initial calls (<paramref name="resultInfo"/> is null).
    /// </param>
    /// <param name="pageSize">
    /// The number of items per page for the initial pagination request. Ignored when <paramref name="resultInfo"/> is provided.
    /// </param>
    /// <param name="updateTotalCount">
    /// Whether to calculate and return the total item count on the initial pagination request.
    /// </param>
    /// <param name="direction">
    /// The sort direction for the initial pagination request. Ignored when <paramref name="resultInfo"/> is provided.
    /// </param>
    /// <returns>
    /// A <see cref="PaginationResultModel{TEntity}"/> containing the page of items.
    /// </returns>
    /// <remarks>
    /// When continuing pagination, jump distances beyond 10 pages in either direction are capped at 10.
    /// </remarks>
    protected async Task<PaginationResultModel<TEntity>> PaginateAsync(IQueryable<TEntity> query, PaginationResultInfo? resultInfo = null,
        long jumpTo = 1, int pageSize = PageSize.SizeDefault, bool updateTotalCount = false, PageSortDirection direction = PageSortDirection.Ascending)
    {
        if (resultInfo == null)
        {
            var firstPageRequest = PaginationRequest.DefaultWith(pageSize, updateTotalCount, direction);
            var firstPageResult = await PaginateAsync(query, firstPageRequest);

            return firstPageResult;
        }

        var pageDifference = resultInfo.PageNumber - jumpTo;
        if (pageDifference > 10)
            jumpTo = resultInfo.PageNumber + 10;
        else if (pageDifference < -10)
            jumpTo = resultInfo.PageNumber - 10;

        if (jumpTo < 1)
            jumpTo = 1;

        var request = pageDifference == 0
            ? resultInfo.RequestRefresh(updateTotalCount)
            : resultInfo.RequestPage(jumpTo, updateTotalCount);

        var result = await PaginateAsync(request);

        return result;
    }

    protected async Task<PaginationResultModel<TEntity>> PaginateAsync(IQueryable<TEntity> query, PaginationRequest request, EntityCreatedFilter? filter = null)
    {
        using var _ = ScopedLog.Measure(this);
        if (!request.PageCursor.IsFirstRequest)
            ValidateEntityId(request.GetCursorId());

        var filteredQuery = filter != null ? Filter(Entities, filter) : query;
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