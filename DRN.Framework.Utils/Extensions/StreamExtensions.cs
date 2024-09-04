using System.Buffers;

namespace DRN.Framework.Utils.Extensions;

public static class StreamExtensions
{
    public static byte[] ToByteArray(this Stream inputStream, long maxSize)
    {
        // If the stream supports seeking, use the stream length to pre-allocate the buffer
        int bytesRead;
        if (inputStream.CanSeek)
        {
            MaxSizeGuard(inputStream.Length, maxSize);

            var buffer = new byte[inputStream.Length];
            var offset = 0;

            while ((bytesRead = inputStream.Read(buffer, offset, buffer.Length - offset)) > 0)
                offset += bytesRead;

            return buffer;
        }

        // For non-seekable streams (e.g., network streams), use ArrayPool to minimize allocations
        var bufferPool = ArrayPool<byte>.Shared;
        var rentedBuffer = bufferPool.Rent(1024); // Rent buffer in 1KB chunks
        using var memoryStream = new MemoryStream();
        var totalBytesRead = 0L;
        try
        {
            // Read the stream in chunks and write to the MemoryStream
            while ((bytesRead = inputStream.Read(rentedBuffer, 0, rentedBuffer.Length)) > 0)
            {
                totalBytesRead += bytesRead;

                MaxSizeGuard(totalBytesRead, maxSize);
                memoryStream.Write(rentedBuffer, 0, bytesRead);
            }

            // Return the final byte array from the MemoryStream
            return memoryStream.ToArray(); // Allocates only once for the final array
        }
        finally
        {
            // Return the rented buffer to the pool
            bufferPool.Return(rentedBuffer);
        }
    }

    private static void MaxSizeGuard(long lenght, long maxSize)
    {
        if (lenght > maxSize)
            throw new InvalidOperationException($"The stream exceeds the maximum allowed size of {maxSize / 1024}KB.");
    }
}