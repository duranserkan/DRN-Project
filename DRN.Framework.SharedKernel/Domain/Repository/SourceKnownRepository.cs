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
    RepositorySettings<TEntity> Settings { get; set; }

    /// <summary>
    /// Gets the stable token for this repository's named cancellation group.
    /// </summary>
    /// <remarks>
    /// The framework repository implementation creates one named scope per concrete repository type by default, so same-type instances share
    /// cancellation within the parent dependency-injection scope. Cancellation remains isolated from the root cancel-all scope and unrelated
    /// repository keys unless the root is canceled. Once cancellation is requested, the repository scope remains canceled for its lifetime.
    /// For one operation only, link the repository token locally:
    /// <code>
    /// using var operationSource = CancellationTokenSource.CreateLinkedTokenSource(
    ///     repository.CancellationToken,
    ///     operationToken);
    /// </code>
    /// </remarks>
    CancellationToken CancellationToken { get; }

    /// <summary>Requests cancellation of this repository scope when <paramref name="token"/> is canceled.</summary>
    /// <remarks>
    /// Cancellation propagates to repositories explicitly sharing the group, but not to the root cancel-all scope or unrelated repositories.
    /// Link operation-only tokens locally instead of merging them.
    /// </remarks>
    void CancelWhen(CancellationToken token);

    /// <summary>Cancels operations using this repository's cancellation scope.</summary>
    /// <remarks>
    /// This affects other repositories only when they explicitly share the same key. It does not cancel the root or unrelated repositories.
    /// </remarks>
    void CancelChanges();

    Task<int> SaveChangesAsync();

    Task<bool> AllAsync(Expression<Func<TEntity, bool>> predicate);
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>>? predicate = null);
    Task<long> CountAsync(Expression<Func<TEntity, bool>>? predicate = null);
    
    /// <summary>
    /// ⚠️ Returns all matching entities in a single query — use with extreme caution.
    /// <para>
    /// ❌ Avoid in public APIs or user-driven queries without size limits.
    /// <para>
    /// </para>
    /// Only safe when: <br/>
    /// • Filters from repository settings (e.g., tenant ID, soft-delete) guarantee a bounded result set, OR <br/>
    /// • The entity count is known to be small (e.g., less than 1000 records).
    /// </para>
    /// </summary>
    Task<TEntity[]> GetAllAsync();
    Task<TEntity> GetAsync(Guid id);
    Task<TEntity> GetAsync(SourceKnownEntityId id);
    Task<TEntity[]> GetAsync(IReadOnlyCollection<Guid> ids);
    Task<TEntity[]> GetAsync(IReadOnlyCollection<SourceKnownEntityId> ids);
    Task<TEntity?> GetOrDefaultAsync(Guid id, bool validate = true);
    Task<TEntity?> GetOrDefaultAsync(SourceKnownEntityId id, bool validate = true);

    void Add(params IReadOnlyCollection<TEntity> entities);
    void Remove(params IReadOnlyCollection<TEntity> entities);

    Task<int> CreateAsync(params IReadOnlyCollection<TEntity> entities);
    Task<int> DeleteAsync(params IReadOnlyCollection<TEntity> entities);
    Task<int> DeleteAsync(params IReadOnlyCollection<Guid> ids);
    Task<int> DeleteAsync(params IReadOnlyCollection<SourceKnownEntityId> ids);

    SourceKnownEntityId GetEntityId(Guid id, bool validate = true);
    SourceKnownEntityId? GetEntityId(Guid? id, bool validate = true);
    SourceKnownEntityId GetEntityId<TOtherEntity>(Guid id) where TOtherEntity : SourceKnownEntity;
    SourceKnownEntityId? GetEntityId<TOtherEntity>(Guid? id) where TOtherEntity : SourceKnownEntity;
    SourceKnownEntityId[] GetEntityIds(IReadOnlyCollection<Guid> ids, bool validate = true);
    SourceKnownEntityId?[] GetEntityIds(IReadOnlyCollection<Guid?> ids, bool validate = true);
    SourceKnownEntityId[] GetEntityIds<TOtherEntity>(IReadOnlyCollection<Guid> ids) where TOtherEntity : SourceKnownEntity;
    SourceKnownEntityId?[] GetEntityIds<TOtherEntity>(IReadOnlyCollection<Guid?> ids) where TOtherEntity : SourceKnownEntity;
    IEnumerable<SourceKnownEntityId> GetEntityIdsAsEnumerable(IEnumerable<Guid> ids, bool validate = true);
    IEnumerable<SourceKnownEntityId?> GetEntityIdsAsEnumerable(IEnumerable<Guid?> ids, bool validate = true);
    IEnumerable<SourceKnownEntityId> GetEntityIdsAsEnumerable<TOtherEntity>(IEnumerable<Guid> ids) where TOtherEntity : SourceKnownEntity;
    IEnumerable<SourceKnownEntityId?> GetEntityIdsAsEnumerable<TOtherEntity>(IEnumerable<Guid?> ids) where TOtherEntity : SourceKnownEntity;

    SourceKnownEntityId ToSecure(SourceKnownEntityId id);
    SourceKnownEntityId ToPlain(SourceKnownEntityId id);

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
