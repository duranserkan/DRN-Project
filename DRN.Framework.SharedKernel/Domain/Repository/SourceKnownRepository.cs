using DRN.Framework.SharedKernel.Domain.Pagination;

namespace DRN.Framework.SharedKernel.Domain.Repository;

public interface ISourceKnownRepository<TEntity>
    where TEntity : AggregateRoot
{
    //todo test cancellation
    //todo test ignore auto includes
    CancellationToken CancellationToken { get; set; }
    void MergeCancellationTokens(CancellationToken other);
    void CancelChanges();

    Task<int> SaveChangesAsync();

    //todo add get or create with cache support
    ValueTask<TEntity> GetAsync(Guid id, bool ignoreAutoIncludes = false);
    Task<TEntity[]> GetAsync(IReadOnlyCollection<Guid> ids, bool ignoreAutoInclude = false);
    ValueTask<TEntity?> GetOrDefaultAsync(Guid id, bool throwException = true, bool ignoreAutoIncludes = false);

    void Add(params IReadOnlyCollection<TEntity> entities);
    void Remove(params IReadOnlyCollection<TEntity> entities);

    Task<int> CreateAsync(params IReadOnlyCollection<TEntity> entities);
    Task<int> DeleteAsync(params IReadOnlyCollection<TEntity> entities);
    Task<int> DeleteAsync(params IReadOnlyCollection<Guid> ids);

    SourceKnownEntityId ValidateEntityId(Guid id, bool throwException = true);
    SourceKnownEntityId[] ValidateEntityIds(IReadOnlyCollection<Guid> ids, bool throwException = true);
    IEnumerable<SourceKnownEntityId> ValidateEntityIdsAsEnumerable(IEnumerable<Guid> ids, bool throwException = true);

    Task<PaginationResult<TEntity>> PaginateAsync(PaginationRequest request, EntityCreatedFilter? filter = null, bool ignoreAutoIncludes = false);
    IAsyncEnumerable<PaginationResult<TEntity>> PaginateAllAsync(PaginationRequest request, EntityCreatedFilter? filter = null, bool ignoreAutoIncludes = false);
}