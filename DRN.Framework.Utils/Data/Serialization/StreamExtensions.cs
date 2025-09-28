using System.Buffers;

namespace DRN.Framework.Utils.Data.Serialization;

public static class StreamExtensions
{
    private const int DefaultBufferSize = 8192; // 8KB is often optimal

    public static async ValueTask<byte[]> ToArrayAsync(this Stream inputStream,
        long maxSize = long.MaxValue, CancellationToken cancellationToken = default)
    {
        var binaryData = await inputStream.ToBinaryDataAsync(maxSize, cancellationToken);
        return binaryData.ToArray();
    }

    public static async ValueTask<BinaryData> ToBinaryDataAsync(this Stream inputStream,
        long maxSize = long.MaxValue, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputStream);

        if (inputStream.CanSeek)
        {
            var originalPosition = inputStream.Position;
            try
            {
                var length = inputStream.Length - inputStream.Position;
                MaxSizeGuard(length, maxSize);

                if (length == 0) return BinaryData.Empty;

                var seekableBuffer = new byte[length];
                await inputStream.ReadExactlyAsync(seekableBuffer, cancellationToken);
                return BinaryData.FromBytes(seekableBuffer);
            }
            finally
            {
                inputStream.Position = originalPosition;
            }
        }

        // For non-seekable streams, we still need the loop approach
        var pool = ArrayPool<byte>.Shared;
        var pooledBuffer = pool.Rent(DefaultBufferSize);

        try
        {
            using var memoryStream = new MemoryStream();
            int bytesRead;
            long totalRead = 0;

            do
            {
                var remainingSpace = Math.Min(pooledBuffer.Length, (int)Math.Max(0, maxSize - totalRead));
                if (remainingSpace == 0) break;

                var readBuffer = pooledBuffer.AsMemory(0, remainingSpace);
                bytesRead = await inputStream.ReadAsync(readBuffer, cancellationToken);

                if (bytesRead <= 0) continue;

                totalRead += bytesRead;
                MaxSizeGuard(totalRead, maxSize);
                await memoryStream.WriteAsync(pooledBuffer.AsMemory(0, bytesRead), cancellationToken);
            } while (bytesRead > 0);

            return BinaryData.FromBytes(memoryStream.ToArray());
        }
        finally
        {
            pool.Return(pooledBuffer);
        }
    }

    private static void MaxSizeGuard(long length, long maxSize)
    {
        if (length > maxSize)
            throw new InvalidOperationException($"The stream exceeds the maximum allowed size of {maxSize:N0} bytes.");
    }
}