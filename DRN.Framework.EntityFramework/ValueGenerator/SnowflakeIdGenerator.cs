using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace DRN.Framework.EntityFramework.ValueGenerator;

public class SnowflakeIdGenerator : ValueGenerator<long>
{
    private const int DEFAULT_NODE_ID_BITS = 10;
    private const int DEFAULT_SEQUENCE_BITS = 12;
    
    private readonly long _nodeId;
    private readonly int _nodeIdBits;
    private readonly int _sequenceBits;
    
    private long _lastTimestamp = -1L;
    private long _sequence = 0L;
    
    private static readonly DateTimeOffset Epoch = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
    //private readonly long _maxNodeId;
    private readonly long _sequenceMask;
    
    private readonly object _lock = new object();

    public SnowflakeIdGenerator(int nodeId, int nodeIdBits = DEFAULT_NODE_ID_BITS, 
        int sequenceBits = DEFAULT_SEQUENCE_BITS)
    {
        _nodeId = nodeId;
        _nodeIdBits = nodeIdBits;
        _sequenceBits = sequenceBits;

        var maxNodeId = (1L << nodeIdBits) - 1;
        _sequenceMask = (1L << sequenceBits) - 1;
        
        if (nodeId > maxNodeId || nodeId < 0)
        {
            throw new ArgumentException($"Node ID must be between 0 and {maxNodeId}");
        }
    }

    public override bool GeneratesTemporaryValues => false;

    public override long Next(EntityEntry entry)
    {
        lock (_lock)
        {
            var timestamp = CurrentTimestamp();

            if (timestamp < _lastTimestamp)
            {
                throw new InvalidOperationException("Clock moved backwards!");
            }

            if (_lastTimestamp == timestamp)
            {
                _sequence = (_sequence + 1) & _sequenceMask;
                if (_sequence == 0)
                {
                    timestamp = WaitNextMillis(_lastTimestamp);
                }
            }
            else
            {
                _sequence = 0;
            }

            _lastTimestamp = timestamp;

            return (timestamp << (_nodeIdBits + _sequenceBits)) 
                   | (_nodeId << _sequenceBits) 
                   | _sequence;
        }
    }

    private long WaitNextMillis(long lastTimestamp)
    {
        var timestamp = CurrentTimestamp();
        while (timestamp <= lastTimestamp)
        {
            timestamp = CurrentTimestamp();
        }
        return timestamp;
    }

    private static long CurrentTimestamp()
    {
        var totalSeconds = (DateTimeOffset.UtcNow - Epoch).Ticks / TimeSpan.TicksPerSecond;
        return (int)totalSeconds;
    }

    public override ValueTask<long> NextAsync(EntityEntry entry, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(Next(entry));
    }
}