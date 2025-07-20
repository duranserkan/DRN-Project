using DRN.Framework.EntityFramework.Context;
using DRN.Framework.SharedKernel;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.SharedKernel.Domain.Repository;
using DRN.Framework.Utils.Entity;
using Microsoft.EntityFrameworkCore;

namespace DRN.Framework.EntityFramework;

//todo test
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
        var rowCount = await Filter(Entities, ids).ExecuteDeleteAsync(CancellationToken);

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


    public async Task<PaginationResult<TEntity>> PaginateAsync(PaginationRequest request, EntityCreatedFilter? filter = null)
        => await PaginateAsync(Entities, request, filter);

    public IAsyncEnumerable<PaginationResult<TEntity>> PaginateAllAsync(PaginationRequest request, EntityCreatedFilter? filter = null)
        => PaginateAllAsync(Entities, request, filter);

    protected async Task<PaginationResult<TEntity>> PaginateAsync(IQueryable<TEntity> query, PaginationRequest request, EntityCreatedFilter? filter = null)
    {
        if (!request.PageCursor.IsFirstRequest)
            ValidateEntityId(request.GetCursorId());

        var filteredQuery = filter != null ? Filter(Entities, filter) : query;
        var result = await Utils.Pagination.GetResultAsync(filteredQuery, request, CancellationToken);

        return result;
    }

    protected async IAsyncEnumerable<PaginationResult<TEntity>> PaginateAllAsync(IQueryable<TEntity> query, PaginationRequest request, EntityCreatedFilter? filter = null)
    {
        PaginationResult<TEntity> result;
        do
        {
            result = await PaginateAsync(query, request, filter);

            yield return result;
        } while (result.HasNext);
    }
    
    protected IQueryable<TEntity> Filter(IQueryable<TEntity> query, params Guid[] ids)
    {
        var sourceKnownIds = ValidateEntityIdsAsEnumerable(ids).Select(sourceKnownEntityId => sourceKnownEntityId.Source.Id).ToArray();
        var filteredQuery = Filter(query, sourceKnownIds);

        return filteredQuery;
    }

    protected IQueryable<TEntity> Filter(IQueryable<TEntity> query, params long[] ids) => query.Where(entity => ids.Contains(entity.Id));

    protected IQueryable<TEntity> Filter(IQueryable<TEntity> query, EntityCreatedFilter filter) => Utils.DateTime.Apply(query, filter);
}