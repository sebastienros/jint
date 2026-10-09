#if NET8_0_OR_GREATER
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Jint.WebApi.Fetch;

// The cache never pulls ahead of a consumer, and publishes only after EOF. Abandoning a response,
// cancellation and a failed read all discard the prefix without turning it into a stored response.
internal sealed class CacheCaptureContent : HttpContent
{
    private readonly HttpContent _source;
    private readonly int _limit;
    private Action<byte[]?>? _finish;

    internal CacheCaptureContent(HttpContent source, int limit, Action<byte[]?> finish)
    {
        _source = source;
        _limit = limit;
        _finish = finish;
        foreach (var h in source.Headers) Headers.TryAddWithoutValidation(h.Key, h.Value);
    }

    private void Finish(byte[]? bytes) => Interlocked.Exchange(ref _finish, null)?.Invoke(bytes);
    protected override bool TryComputeLength(out long length)
    {
        length = _source.Headers.ContentLength ?? 0;
        return _source.Headers.ContentLength is not null;
    }
    protected override async Task<Stream> CreateContentReadStreamAsync()
        => await CreateContentReadStreamAsync(CancellationToken.None).ConfigureAwait(false);
    protected override async Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken)
        => new CaptureStream(await _source.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), _limit, _source.Headers.ContentLength, Finish);
    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        => await SerializeToStreamAsync(stream, context, CancellationToken.None).ConfigureAwait(false);
    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        var source = await ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await source.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { Finish(null); _source.Dispose(); }
        base.Dispose(disposing);
    }

    private sealed class CaptureStream(Stream source, int limit, long? declaredLength, Action<byte[]?> finish) : Stream
    {
        private MemoryStream? _buffer = new();
        private bool _done;
        private void Capture(ReadOnlySpan<byte> bytes)
        {
            if (_done) return;
            if (bytes.Length == 0)
            {
                var buffer = _buffer;
                _buffer = null;
                _done = true;
                try { finish(buffer is not null && (declaredLength is null || declaredLength == buffer.Length) ? buffer.ToArray() : null); }
                finally { buffer?.Dispose(); }
            }
            else if (_buffer is { } buffer)
            {
                if (buffer.Length > limit - bytes.Length)
                {
                    buffer.Dispose();
                    _buffer = null;
                    _done = true;
                    finish(null);
                }
                else
                {
                    // Bound the geometric capacity as well as the logical length.
                    var needed = (int) buffer.Length + bytes.Length;
                    if (needed > buffer.Capacity) buffer.Capacity = Math.Min(limit, Math.Max(needed, Math.Max(256, buffer.Capacity * 2)));
                    buffer.Write(bytes);
                }
            }
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.Length == 0) return 0;
            try
            {
                var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                Capture(buffer.Span[..read]);
                return read;
            }
            catch { Dispose(); throw; }
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count == 0) return 0;
            try { var read = source.Read(buffer, offset, count); Capture(buffer.AsSpan(offset, read)); return read; }
            catch { Dispose(); throw; }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!_done) { _done = true; finish(null); }
                _buffer?.Dispose();
                _buffer = null;
                source.Dispose();
            }
            base.Dispose(disposing);
        }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
#endif
