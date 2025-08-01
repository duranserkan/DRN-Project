namespace DRN.Framework.SharedKernel.Domain.Repository;

/// <summary>
/// Settings for default public members of SourceKnownRepositories
/// </summary>
public class RepositorySettings
{
    public bool IgnoreAutoIncludes { get; set; }
    public bool AsNoTracking { get; set; }
}