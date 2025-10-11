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

    Task<bool> AllAsync(Expression<Func<TEntity, bool>> predicate);
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>>? predicate = null);
    Task<long> CountAsync(Expression<Func<TEntity, bool>>? predicate = null);

    //todo add get or create with cache support
    ValueTask<TEntity> GetAsync(Guid id);
    Task<TEntity[]> GetAsync(IReadOnlyCollection<Guid> ids);
    ValueTask<TEntity?> GetOrDefaultAsync(Guid id, bool throwException = true);

    void Add(params IReadOnlyCollection<TEntity> entities);
    void Remove(params IReadOnlyCollection<TEntity> entities);

    Task<int> CreateAsync(params IReadOnlyCollection<TEntity> entities);
    Task<int> DeleteAsync(params IReadOnlyCollection<TEntity> entities);
    Task<int> DeleteAsync(params IReadOnlyCollection<Guid> ids);

    SourceKnownEntityId GetEntityId(Guid id, bool throwException = true);
    SourceKnownEntityId GetEntityId<TOtherEntity>(Guid id) where TOtherEntity : SourceKnownEntity;
    SourceKnownEntityId[] GetEntityIds(IReadOnlyCollection<Guid> ids, bool throwException = true);
    SourceKnownEntityId[] GetEntityIds<TOtherEntity>(IReadOnlyCollection<Guid> ids) where TOtherEntity : SourceKnownEntity;
    IEnumerable<SourceKnownEntityId> GetEntityIdsAsEnumerable(IEnumerable<Guid> ids, bool throwException = true);
    IEnumerable<SourceKnownEntityId> GetEntityIdsAsEnumerable<TOtherEntity>(IEnumerable<Guid> ids) where TOtherEntity : SourceKnownEntity;

    Task<PaginationResultModel<TEntity>> PaginateAsync(PaginationRequest request, EntityCreatedFilter? filter = null);

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
    Task<PaginationResultModel<TEntity>> PaginateAsync(
        PaginationResultInfo? resultInfo = null, long jumpTo = 1, int pageSize = -1, int maxSize = -1,
        PageSortDirection direction = PageSortDirection.None, long totalCount = -1, bool updateTotalCount = false);

    IAsyncEnumerable<PaginationResultModel<TEntity>> PaginateAllAsync(PaginationRequest request, EntityCreatedFilter? filter = null);
}