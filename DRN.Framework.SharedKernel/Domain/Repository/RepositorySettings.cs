using System.Linq.Expressions;

namespace DRN.Framework.SharedKernel.Domain.Repository;

/// <summary>
/// Settings for default public members of SourceKnownRepositories
/// </summary>
public class RepositorySettings<TEntity>
    where TEntity : AggregateRoot
{
    public bool IgnoreAutoIncludes { get; set; }
    public bool AsNoTracking { get; set; }
    
    private readonly Dictionary<string, Expression<Func<TEntity, bool>>> _filters = [];
    public IReadOnlyDictionary<string, Expression<Func<TEntity, bool>>> Filters => _filters;

    public void AddFilter(string name, Expression<Func<TEntity, bool>> filter) => _filters[name] = filter;
    public bool RemoveFilter(string name) => _filters.Remove(name);
    public void ClearFilters() => _filters.Clear();
}