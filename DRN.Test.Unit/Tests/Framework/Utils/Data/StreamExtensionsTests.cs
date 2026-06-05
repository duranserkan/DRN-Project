using DRN.Framework.Utils.Data.Serialization;

namespace DRN.Test.Unit.Tests.Framework.Utils.Data;

public class StreamExtensionsTests
{
    [Theory]
    [DataInlineUnit]
    public async Task ToArrayAsync_Should_Read_NonSeekable_Stream_At_MaxSize(DrnTestContextUnit _)
    {
        var payload = Enumerable.Range(0, 10).Select(value => (byte)value).ToArray();
        await using var stream = new NonSeekableReadStream(payload);

        var bytes = await stream.ToArrayAsync(payload.Length);

        bytes.Should().Equal(payload);
    }

    [Theory]
    [DataInlineUnit]
    public async Task ToArrayAsync_Should_Reject_NonSeekable_Stream_Over_MaxSize(DrnTestContextUnit _)
    {
        var payload = Enumerable.Range(0, 11).Select(value => (byte)value).ToArray();
        await using var stream = new NonSeekableReadStream(payload);

        var read = async () => await stream.ToArrayAsync(10);

        await read.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*maximum allowed size*");
    }

    private sealed class NonSeekableReadStream(byte[] payload) : Stream
    {
        private readonly MemoryStream _inner = new(payload);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
            => _inner.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => _inner.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _inner.Dispose();

            base.Dispose(disposing);
        }
    }
}
