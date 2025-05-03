using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Ids;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace DRN.Framework.EntityFramework.Context.Interceptors;

public class SourceKnownIdValueGenerator : ValueGenerator<long>
{
    private const string NextId = nameof(SourceKnownIdUtils.Next);

    private ISourceKnownIdUtils? _idUtils;
    private readonly Lock _lock = new();

    public override long Next(EntityEntry entry)
    {
        if (entry is not { Entity: Entity entity }) return 0;

        if (_idUtils == null)
            lock (_lock)
                _idUtils ??= entry.Context.GetService<ISourceKnownIdUtils>();

        if (entity.Id == 0)
            entity.Id = (long)_idUtils.InvokeGenericMethod(NextId, entity.GetType())!;

        return entity.Id;
    }

    public override bool GeneratesTemporaryValues => false;
}