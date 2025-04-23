using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SourceKnownIdUtils = DRN.Framework.Utils.Ids.SourceKnownIdUtils;

namespace DRN.Framework.EntityFramework.Context.Interceptors;

public interface IDrnSaveChangesInterceptor : ISaveChangesInterceptor, ISingletonInterceptor;

[Singleton<IDrnSaveChangesInterceptor>]
public class DrnSaveChangesInterceptor(ISourceKnownIdUtils idUtils, ISourceKnownEntityIdUtils entityIdUtils) : IDrnSaveChangesInterceptor
{
    private const string NextId = nameof(SourceKnownIdUtils.Next);

    public ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        MarkEntities(eventData, idUtils, entityIdUtils);

        return new ValueTask<InterceptionResult<int>>(result);
    }

    public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        MarkEntities(eventData, idUtils, entityIdUtils);

        return result;
    }

    private static void MarkEntities(DbContextEventData eventData, ISourceKnownIdUtils idUtils, ISourceKnownEntityIdUtils entityIdUtils)
    {
        foreach (var entityEntry in eventData.Context!.ChangeTracker.Entries())
        {
            if (entityEntry is not { Entity: Entity entity })
                continue;

            switch (entityEntry.State)
            {
                case EntityState.Added:
                    if (entity.Id == 0)
                        entity.Id = (long)idUtils.InvokeGenericMethod(NextId, entity.GetType())!;
                    if (entity.EntityId == Guid.Empty)
                        entity.EntityIdSource = entityIdUtils.Generate(entity);
                    entity.MarkAsCreated();
                    break;
                case EntityState.Modified:
                    entity.MarkAsModified();
                    break;
                case EntityState.Deleted:
                    entity.MarkAsDeleted();
                    break;
            }
        }
    }
}