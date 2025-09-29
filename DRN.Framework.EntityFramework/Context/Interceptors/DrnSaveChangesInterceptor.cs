using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Entity;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Ids;
using DRN.Framework.Utils.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DRN.Framework.EntityFramework.Context.Interceptors;

public interface IDrnSaveChangesInterceptor : ISaveChangesInterceptor, ISingletonInterceptor;

[Singleton<IDrnSaveChangesInterceptor>]
public class DrnSaveChangesInterceptor(IEntityUtils entityUtils) : IDrnSaveChangesInterceptor
{
    private const string NextId = nameof(SourceKnownIdUtils.Next);

    public ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        MarkEntities(eventData, entityUtils);

        return new ValueTask<InterceptionResult<int>>(result);
    }

    public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        MarkEntities(eventData, entityUtils);

        return result;
    }

    private static void MarkEntities(DbContextEventData eventData, IEntityUtils entityUtils)
    {
        foreach (var entityEntry in eventData.Context!.ChangeTracker.Entries())
        {
            if (entityEntry is not { Entity: SourceKnownEntity entity })
                continue;

            switch (entityEntry.State)
            {
                case EntityState.Added:
                    if (entity.Id == 0)
                        entity.Id = (long)entityUtils.Id.InvokeGenericMethod(NextId, entity.GetType())!;
                    if (entity.EntityId == Guid.Empty)
                        entity.EntityIdSource = entityUtils.EntityId.Generate(entity);
                    
                    entity.IdFactory = entityUtils.EntityId.Generate;
                    entity.Parser = entityUtils.EntityId.Parse;
                    entity.Validator = entityUtils.EntityId.Validate;
                    entity.ModifiedAt = entity.CreatedAt;
                    entity.MarkAsCreated();
                    break;
                case EntityState.Modified:
                    entity.ModifiedAt = DateTimeProvider.UtcNow;
                    entity.MarkAsModified();
                    break;
                case EntityState.Deleted:
                    entity.MarkAsDeleted();
                    break;
            }
        }
    }
}