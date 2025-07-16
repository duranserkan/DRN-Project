using DRN.Framework.EntityFramework.Context;
using DRN.Framework.SharedKernel;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.Utils.Entity;
using Microsoft.EntityFrameworkCore;

namespace DRN.Framework.EntityFramework;

//todo test
//todo merge date filters
public abstract class SourceKnownRepository<TEntity, TContext>(TContext context, IEntityUtils utils) : ISourceKnownRepository<TEntity>
    where TContext : DbContext, IDrnContext
    where TEntity : AggregateRoot
{
    private static readonly byte EntityTypeId = SourceKnownEntity.GetEntityTypeId<TEntity>();

    protected DbSet<TEntity> Entities { get; } = context.Set<TEntity>();
    protected IEntityUtils Utils { get; } = utils;


    private CancellationToken _cancellationToken = CancellationToken.None;

    public CancellationToken CancellationToken
    {
        get => _cancellationToken == CancellationToken.None ? Utils.Cancellation.Token : _cancellationToken;
        set => _cancellationToken = value;
    }

    public void MergeTokens(CancellationToken other) => Utils.Cancellation.Merge(other);

    public void CancelChanges() => Utils.Cancellation.Cancel();

    /// <summary>
    /// Saves all changes made in this context to the database.
    /// </summary>
    /// <returns>The number of state entries written to the database</returns>
    public async Task<int> SaveChangesAsync()
    {
        var rowCount = await context.SaveChangesAsync(CancellationToken);

        return rowCount;
    }

    /// <summary>
    /// Finds an entity with the given source known id
    /// </summary>
    /// <exception cref="NotFoundException">Thrown when entity not found</exception>
    /// <exception cref="ValidationException">Thrown when id is invalid or doesn't match the repository entity type</exception>
    public async ValueTask<TEntity> GetAsync(Guid id)
    {
        var entityId = ValidateEntityId(id);
        var entity = await Entities.FindAsync(entityId.Source.Id, CancellationToken)
                     ?? throw new NotFoundException($"{typeof(TEntity).FullName} not found: {entityId}");

        return entity;
    }

    /// <summary>
    /// Begins tracking the given entities, and any other reachable entities that are not already being tracked,
    /// in the Added state such that they will be inserted into the database when SaveChanges() is called.
    /// </summary>
    public void Add(params TEntity[] entities) => Entities.AddRange(entities);

    /// <summary>
    /// Begins tracking the given entities in the Deleted state such that they will be removed from the database when SaveChanges() is called.
    /// </summary>
    public void Remove(params TEntity[] entities) => Entities.RemoveRange(entities);


    /// <summary>
    /// equivalent to calling Add + SaveChangesAsync
    /// </summary>
    /// <returns>The number of state entries written to the database</returns>
    public async Task<int> CreateAsync(params TEntity[] entities)
    {
        Add(entities);
        var rowCount = await context.SaveChangesAsync(CancellationToken);

        return rowCount;
    }

    /// <summary>
    /// equivalent to calling Remove + SaveChangesAsync
    /// </summary>
    /// <returns>The number of state entries written to the database</returns>
    public async Task<int> DeleteAsync(params TEntity[] entities)
    {
        Remove(entities);
        var rowCount = await context.SaveChangesAsync(CancellationToken);

        return rowCount;
    }

    /// <summary>
    /// Directly deletes entities from the database without fetching first.
    /// </summary>
    /// <returns>The total number of rows deleted in the database.</returns>
    public async Task<int> DeleteAsync(params Guid[] ids)
    {
        var sourceKnownIds = ValidateEntityIdsAsEnumerable(ids).Select(sourceKnownEntityId => sourceKnownEntityId.Source.Id).ToArray();
        var rowCount = await Entities.Where(entity => sourceKnownIds.Contains(entity.Id)).ExecuteDeleteAsync(CancellationToken);

        return rowCount;
    }

    /// <exception cref="ValidationException">Thrown when id is invalid or doesn't match the repository entity type</exception>
    public SourceKnownEntityId ValidateEntityId(Guid id, bool throwException = true)
        => throwException ? Utils.EntityId.Validate(id, EntityTypeId) : Utils.EntityId.Parse(id);

    /// <exception cref="ValidationException">Thrown when id is invalid or doesn't match the repository entity type</exception>
    public SourceKnownEntityId[] ValidateEntityIds(IEnumerable<Guid> ids, bool throwException = true)
        => ValidateEntityIdsAsEnumerable(ids, throwException).ToArray();

    public IEnumerable<SourceKnownEntityId> ValidateEntityIdsAsEnumerable(IEnumerable<Guid> ids, bool throwException = true)
        => ids.Select(id => ValidateEntityId(id, throwException));


    public async Task<PaginationResult<TEntity>> PaginateAsync(PaginationRequest request)
        => await PaginateAsync(Entities, request);

    public async Task<PaginationResult<TEntity>> PaginateCreatedBeforeAsync(PaginationRequest request, DateTimeOffset after, bool inclusive = true)
        => await PaginateCreatedBeforeAsync(Entities, request, after, inclusive);

    public async Task<PaginationResult<TEntity>> PaginateCreatedAfterAsync(PaginationRequest request, DateTimeOffset before, bool inclusive = true)
        => await PaginateCreatedAfterAsync(Entities, request, before, inclusive);

    public async Task<PaginationResult<TEntity>> PaginateCreatedBetweenAsync(PaginationRequest request, DateTimeOffset before, DateTimeOffset after,
        bool inclusive = true)
        => await PaginateCreatedBetweenAsync(Entities, request, before, after, inclusive);

    public async Task<PaginationResult<TEntity>> PaginateCreatedOutsideAsync(PaginationRequest request, DateTimeOffset before, DateTimeOffset after,
        bool inclusive = true)
        => await PaginateCreatedOutsideAsync(Entities, request, before, after, inclusive);


    public IAsyncEnumerable<PaginationResult<TEntity>> PaginateAllAsync(PaginationRequest request)
        => PaginateAllAsync(Entities, request);

    public IAsyncEnumerable<PaginationResult<TEntity>> PaginateAllCreatedBeforeAsync(PaginationRequest request, DateTimeOffset after, bool inclusive = true)
        => PaginateAllCreatedBeforeAsync(Entities, request, after, inclusive);

    public IAsyncEnumerable<PaginationResult<TEntity>> PaginateAllCreatedAfterAsync(PaginationRequest request, DateTimeOffset before, bool inclusive = true)
        => PaginateAllCreatedAfterAsync(Entities, request, before, inclusive);

    public IAsyncEnumerable<PaginationResult<TEntity>> PaginateAllCreatedBetweenAsync(PaginationRequest request, DateTimeOffset before, DateTimeOffset after,
        bool inclusive = true)
        => PaginateAllCreatedBetweenAsync(Entities, request, before, after, inclusive);

    public IAsyncEnumerable<PaginationResult<TEntity>> PaginateAllCreatedOutsideAsync(PaginationRequest request, DateTimeOffset before, DateTimeOffset after,
        bool inclusive = true)
        => PaginateAllCreatedOutsideAsync(Entities, request, before, after, inclusive);

    protected async Task<PaginationResult<TEntity>> PaginateAsync(IQueryable<TEntity> query, PaginationRequest? request = null)
    {
        request ??= PaginationRequest.Default;

        if (!request.PageCursor.IsFirstRequest)
            ValidateEntityId(request.GetCursorId());

        var result = await Utils.Pagination.GetResultAsync(query, request, CancellationToken);
        return result;
    }

    protected async IAsyncEnumerable<PaginationResult<TEntity>> PaginateAllAsync(IQueryable<TEntity> query, PaginationRequest request)
    {
        PaginationResult<TEntity> result;
        do
        {
            result = await Utils.Pagination.GetResultAsync(query, request, CancellationToken);

            yield return result;
        } while (result.HasNext);
    }

    protected async Task<PaginationResult<TEntity>> PaginateCreatedBeforeAsync(
        IQueryable<TEntity> query,
        PaginationRequest request,
        DateTimeOffset dateAfterCreation,
        bool inclusive = true)
    {
        var filteredQuery = Utils.DateTime.CreatedBefore(query, dateAfterCreation, inclusive);
        var result = await PaginateAsync(filteredQuery, request);

        return result;
    }

    protected IAsyncEnumerable<PaginationResult<TEntity>> PaginateAllCreatedBeforeAsync(
        IQueryable<TEntity> query,
        PaginationRequest request,
        DateTimeOffset dateAfterCreation,
        bool inclusive = true)
    {
        var filteredQuery = Utils.DateTime.CreatedBefore(query, dateAfterCreation, inclusive);

        return PaginateAllAsync(filteredQuery, request);
    }

    protected async Task<PaginationResult<TEntity>> PaginateCreatedAfterAsync(
        IQueryable<TEntity> query,
        PaginationRequest request,
        DateTimeOffset dateBeforeCreation,
        bool inclusive = true)
    {
        var filteredQuery = Utils.DateTime.CreatedAfter(query, dateBeforeCreation, inclusive);
        var result = await PaginateAsync(filteredQuery, request);

        return result;
    }

    protected IAsyncEnumerable<PaginationResult<TEntity>> PaginateAllCreatedAfterAsync(
        IQueryable<TEntity> query,
        PaginationRequest request,
        DateTimeOffset dateBeforeCreation,
        bool inclusive = true)
    {
        var filteredQuery = Utils.DateTime.CreatedAfter(query, dateBeforeCreation, inclusive);

        return PaginateAllAsync(filteredQuery, request);
    }

    protected async Task<PaginationResult<TEntity>> PaginateCreatedBetweenAsync(
        IQueryable<TEntity> query,
        PaginationRequest request,
        DateTimeOffset dateBeforeCreation,
        DateTimeOffset dateAfterCreation,
        bool inclusive = true)
    {
        var filteredQuery = Utils.DateTime.CreatedBetween(query, dateBeforeCreation, dateAfterCreation, inclusive);
        var result = await PaginateAsync(filteredQuery, request);

        return result;
    }

    protected IAsyncEnumerable<PaginationResult<TEntity>> PaginateAllCreatedBetweenAsync(
        IQueryable<TEntity> query,
        PaginationRequest request,
        DateTimeOffset dateBeforeCreation,
        DateTimeOffset dateAfterCreation,
        bool inclusive = true)
    {
        var filteredQuery = Utils.DateTime.CreatedBetween(query, dateBeforeCreation, dateAfterCreation, inclusive);

        return PaginateAllAsync(filteredQuery, request);
    }

    protected async Task<PaginationResult<TEntity>> PaginateCreatedOutsideAsync(
        IQueryable<TEntity> query,
        PaginationRequest request,
        DateTimeOffset before,
        DateTimeOffset after,
        bool inclusive = true)
    {
        var filteredQuery = Utils.DateTime.CreatedOutside(query, before, after, inclusive);
        var result = await PaginateAsync(filteredQuery, request);

        return result;
    }

    protected IAsyncEnumerable<PaginationResult<TEntity>> PaginateAllCreatedOutsideAsync(
        IQueryable<TEntity> query,
        PaginationRequest request,
        DateTimeOffset before,
        DateTimeOffset after,
        bool inclusive = true)
    {
        var filteredQuery = Utils.DateTime.CreatedOutside(query, before, after, inclusive);

        return PaginateAllAsync(filteredQuery, request);
    }
}