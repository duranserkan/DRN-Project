using System.Linq.Expressions;
using DRN.Framework.SharedKernel.Domain.Pagination;

namespace DRN.Framework.SharedKernel.Domain.Repository;

/// <summary>
/// Represents a repository interface that manages entities of type <typeparamref name="TEntity"/>.
/// Provides methods for CRUD operations, entity retrieval, pagination, and cancellation token management.
/// </summary>
/// <remarks>
/// Entity updates, additional filtering logic, and query includes (e.g., <c>Include</c> statements) are the responsibility of concrete subclasses.
/// </remarks>
public interface ISourceKnownRepository<TEntity>
    where TEntity : AggregateRoot
{
    /// <summary>
    /// Settings for default public members of SourceKnownRepositories
    /// </summary>
    RepositorySettings Settings { get; set; }
    
    //todo test cancellation
    //todo test ignore auto includes
    CancellationToken CancellationToken { get; set; }
    void MergeCancellationTokens(CancellationToken other);
    void CancelChanges();

    Task<int> SaveChangesAsync();

    Task<bool> AnyAsync();
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate);
    Task<long> CountAsync();
    Task<long> CountAsync(Expression<Func<TEntity, bool>> predicate);
    
    //todo add get or create with cache support
    ValueTask<TEntity> GetAsync(Guid id);
    Task<TEntity[]> GetAsync(IReadOnlyCollection<Guid> ids);
    ValueTask<TEntity?> GetOrDefaultAsync(Guid id, bool throwException = true);

    void Add(params IReadOnlyCollection<TEntity> entities);
    void Remove(params IReadOnlyCollection<TEntity> entities);

    Task<int> CreateAsync(params IReadOnlyCollection<TEntity> entities);
    Task<int> DeleteAsync(params IReadOnlyCollection<TEntity> entities);
    Task<int> DeleteAsync(params IReadOnlyCollection<Guid> ids);

    SourceKnownEntityId ValidateEntityId(Guid id, bool throwException = true);
    SourceKnownEntityId[] ValidateEntityIds(IReadOnlyCollection<Guid> ids, bool throwException = true);
    IEnumerable<SourceKnownEntityId> ValidateEntityIdsAsEnumerable(IEnumerable<Guid> ids, bool throwException = true);

    Task<PaginationResultModel<TEntity>> PaginateAsync(PaginationRequest request, EntityCreatedFilter? filter = null);

    /// <summary>
    /// Can start initial pagination with system defaults if result info not provided.
    /// If a previous result is provided, then navigates accordingly by using creating a cursor by it
    /// </summary>
    /// <param name="resultInfo">Previous request result</param>
    /// <param name="jumpTo">To be jumped page when the NavigationDirection is Jump </param>
    /// <param name="pageSize">
    /// The number of items per page. Used only when <paramref name="resultInfo"/> is null.
    /// </param>
    /// <param name="updateTotalCount">
    /// Whether to calculate and return the total number of items. Used only when <paramref name="resultInfo"/> is null.
    /// </param>
    /// <param name="direction">
    /// The sorting direction to use for pagination. Used only when <paramref name="resultInfo"/> is null.
    /// </param>
    /// <returns>
    /// A <see cref="PaginationResultModel{TEntity}"/> containing the paginated results.
    /// </returns>
    /// <remarks>
    /// The maximum allowed jump distance is limited to 10 pages in either direction.
    /// </remarks>
    Task<PaginationResultModel<TEntity>> PaginateAsync(PaginationResultInfo? resultInfo = null,
        long jumpTo = 1, int pageSize = PageSize.SizeDefault, bool updateTotalCount = false, PageSortDirection direction = PageSortDirection.Ascending);

    IAsyncEnumerable<PaginationResultModel<TEntity>> PaginateAllAsync(PaginationRequest request, EntityCreatedFilter? filter = null);
}