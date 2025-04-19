using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Generic;
using DRN.Framework.Utils.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DRN.Framework.EntityFramework.Context.Interceptors;

public interface IDrnSaveChangesInterceptor : ISaveChangesInterceptor;

[Singleton<DrnSaveChangesInterceptor>]
public class DrnSaveChangesInterceptor(SourceKnownIdUtils idUtils) : IDrnSaveChangesInterceptor
{
    private const string NextId = nameof(SourceKnownIdUtils.Next);

    public ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        MarkEntities(eventData, idUtils);

        return new ValueTask<InterceptionResult<int>>(result);
    }

    public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        MarkEntities(eventData, idUtils);

        return result;
    }

    private static void MarkEntities(DbContextEventData eventData, SourceKnownIdUtils idUtils)
    {
        foreach (var entityEntry in eventData.Context!.ChangeTracker.Entries())
        {
            if (entityEntry is not { Entity: Entity entity })
                continue;

            switch (entityEntry.State)
            {
                case EntityState.Added:
                    entity.MarkAsCreated((long)idUtils.InvokeGenericMethod(NextId, entity.GetType())!);
                    entity.Id = (long)idUtils.InvokeGenericMethod(NextId, entity.GetType())!;
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