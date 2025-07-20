using DRN.Framework.SharedKernel.Domain.Pagination;

namespace DRN.Framework.SharedKernel.Domain.Repository;

public interface ISourceKnownRepository<TEntity>
    where TEntity : AggregateRoot
{
    CancellationToken CancellationToken { get; set; }
    void MergeTokens(CancellationToken other);
    void CancelChanges();

    Task<int> SaveChangesAsync();

    //todo add get or create with cache support
    ValueTask<TEntity> GetAsync(Guid id);

    void Add(params TEntity[] entities);
    void Remove(params TEntity[] entities);

    Task<int> CreateAsync(params TEntity[] entities);
    Task<int> DeleteAsync(params TEntity[] entities);
    Task<int> DeleteAsync(params Guid[] id);

    SourceKnownEntityId ValidateEntityId(Guid id, bool throwException = true);
    SourceKnownEntityId[] ValidateEntityIds(IEnumerable<Guid> ids, bool throwException = true);
    IEnumerable<SourceKnownEntityId> ValidateEntityIdsAsEnumerable(IEnumerable<Guid> ids, bool throwException = true);
    
    Task<PaginationResult<TEntity>> PaginateAsync(PaginationRequest request, EntityCreatedFilter? filter = null);
    IAsyncEnumerable<PaginationResult<TEntity>> PaginateAllAsync(PaginationRequest request, EntityCreatedFilter? filter = null);
}