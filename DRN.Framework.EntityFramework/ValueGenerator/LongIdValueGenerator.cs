using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace DRN.Framework.EntityFramework.ValueGenerator;

public class LongIdValueGenerator : ValueGenerator<long>
{
    private static long _currentValue;

    public override bool GeneratesTemporaryValues => false;

    public override long Next(EntityEntry entry)
    {
        return Interlocked.Increment(ref _currentValue);
    }

    public override ValueTask<long> NextAsync(EntityEntry entry, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(Next(entry));
    }
}