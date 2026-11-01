using System.Buffers;
using System.Text.Json;

namespace DRN.Framework.Utils.Logging.Diagnostics;

// Discard output after each writer advance. Counts retain the serializer's exact escaping rules.
internal sealed class DiagnosticJsonSize : IDisposable
{
    [ThreadStatic] private static DiagnosticJsonSize? _cached;
    private readonly DiscardingBuffer _buffer = new();
    private readonly Utf8JsonWriter _writer;

    private DiagnosticJsonSize() => _writer = new Utf8JsonWriter(_buffer);

    internal static DiagnosticJsonSize Rent()
    {
        var result = _cached;
        _cached = null; // Reentrant serialization must use a different writer.
        return result ?? new DiagnosticJsonSize();
    }

    internal int Measure<T>(T value)
    {
        _writer.Reset(_buffer);
        JsonSerializer.Serialize(_writer, value);
        return Complete();
    }

    internal int MeasureString(ReadOnlySpan<char> value)
    {
        _writer.Reset(_buffer);
        _writer.WriteStringValue(value);
        return Complete();
    }

    // Reserve both the property separator and an entry delimiter, including for the first entry.
    internal int MeasureEntry(string key, object value) => Measure(key) + Measure(value) + 2;

    private int Complete()
    {
        _writer.Flush();
        return checked((int)_writer.BytesCommitted);
    }

    public void Dispose() => Return(this);

    private static void Return(DiagnosticJsonSize sizing)
    {
        sizing._writer.Reset(sizing._buffer);
        sizing._buffer.Release();
        if (_cached == null) _cached = sizing;
        else sizing._writer.Dispose();
    }

    private sealed class DiscardingBuffer : IBufferWriter<byte>
    {
        private byte[]? _bytes;

        public void Advance(int count)
        {
            if (count < 0 || count > (_bytes?.Length ?? 0)) throw new ArgumentOutOfRangeException(nameof(count));
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
            var length = Math.Max(256, sizeHint);
            if (_bytes == null || _bytes.Length < length)
            {
                Release();
                _bytes = ArrayPool<byte>.Shared.Rent(length);
            }
            return _bytes;
        }

        public Span<byte> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;

        internal void Release()
        {
            if (_bytes == null) return;
            ArrayPool<byte>.Shared.Return(_bytes, clearArray: true);
            _bytes = null;
        }
    }
}
