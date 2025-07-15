using DRN.Framework.SharedKernel.Domain.Pagination;

namespace DRN.Framework.SharedKernel.Domain;

public interface ISourceKnownRepository<TEntity>
    where TEntity : AggregateRoot
{
    CancellationToken CancellationToken { get; set; }
    void MergeTokens(CancellationToken other);
    void CancelChanges();

    Task<int> SaveChangesAsync();

    //todo add get or create with cache support
    ValueTask<TEntity> GetAsync(Guid id);
    Task<PaginationResult<TEntity>> PaginateAsync(PaginationRequest request);
    Task<PaginationResult<TEntity>> PaginateCreatedBeforeAsync(PaginationRequest request, DateTimeOffset after, bool inclusive = true);
    Task<PaginationResult<TEntity>> PaginateCreatedAfterAsync(PaginationRequest request, DateTimeOffset before, bool inclusive = true);
    Task<PaginationResult<TEntity>> PaginateCreatedBetweenAsync(PaginationRequest request, DateTimeOffset before, DateTimeOffset after, bool inclusive = true);
    Task<PaginationResult<TEntity>> PaginateCreatedOutsideAsync(PaginationRequest request, DateTimeOffset before, DateTimeOffset after, bool inclusive = true);

    IAsyncEnumerable<PaginationResult<TEntity>> PaginateAllAsync(PaginationRequest request);
    IAsyncEnumerable<PaginationResult<TEntity>> PaginateAllCreatedBeforeAsync(PaginationRequest request, DateTimeOffset after, bool inclusive = true);
    IAsyncEnumerable<PaginationResult<TEntity>> PaginateAllCreatedAfterAsync(PaginationRequest request, DateTimeOffset before, bool inclusive = true);

    IAsyncEnumerable<PaginationResult<TEntity>> PaginateAllCreatedBetweenAsync(PaginationRequest request, DateTimeOffset before, DateTimeOffset after,
        bool inclusive = true);

    IAsyncEnumerable<PaginationResult<TEntity>> PaginateAllCreatedOutsideAsync(PaginationRequest request, DateTimeOffset before, DateTimeOffset after,
        bool inclusive = true);

    void Add(params TEntity[] entities);
    void Remove(params TEntity[] entities);

    Task<int> CreateAsync(params TEntity[] entities);
    Task<int> DeleteAsync(params TEntity[] entities);
    Task<int> DeleteAsync(params Guid[] id);

    SourceKnownEntityId ValidateEntityId(Guid id, bool throwException = true);
    SourceKnownEntityId[] ValidateEntityIds(IEnumerable<Guid> ids, bool throwException = true);
    IEnumerable<SourceKnownEntityId> ValidateEntityIdsAsEnumerable(IEnumerable<Guid> ids, bool throwException = true);
}