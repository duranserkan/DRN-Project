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
    public Expression<Func<TEntity, bool>>? DefaultFilter { get; set; }
}