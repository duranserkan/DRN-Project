using DRN.Framework.SharedKernel.Domain.Pagination;

namespace DRN.Framework.SharedKernel.Domain.Repository;

public interface ISourceKnownRepository<TEntity>
    where TEntity : AggregateRoot
{
    CancellationToken CancellationToken { get; set; }
    void MergeCancellationTokens(CancellationToken other);
    void CancelChanges();

    Task<int> SaveChangesAsync();

    //todo add get or create with cache support
    ValueTask<TEntity> GetAsync(Guid id);
    ValueTask<TEntity?> GetOrDefaultAsync(Guid id, bool throwException = true);
    Task<TEntity[]> GetAsync(IReadOnlyCollection<Guid> ids);

    void Add(params IReadOnlyCollection<TEntity> entities);
    void Remove(params IReadOnlyCollection<TEntity> entities);

    Task<int> CreateAsync(params IReadOnlyCollection<TEntity> entities);
    Task<int> DeleteAsync(params IReadOnlyCollection<TEntity> entities);
    Task<int> DeleteAsync(params IReadOnlyCollection<Guid> id);

    SourceKnownEntityId ValidateEntityId(Guid id, bool throwException = true);
    SourceKnownEntityId[] ValidateEntityIds(IReadOnlyCollection<Guid> ids, bool throwException = true);
    IEnumerable<SourceKnownEntityId> ValidateEntityIdsAsEnumerable(IEnumerable<Guid> ids, bool throwException = true);

    Task<PaginationResult<TEntity>> PaginateAsync(PaginationRequest request, EntityCreatedFilter? filter = null);
    IAsyncEnumerable<PaginationResult<TEntity>> PaginateAllAsync(PaginationRequest request, EntityCreatedFilter? filter = null);
}