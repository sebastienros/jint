#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Jint.WebApi.Fetch;

/// <summary>
/// The memory allowance a paused response's body is read against, owned by the host rather than by Jint.
/// </summary>
/// <remarks>
/// <para>
/// <b>Engine-free and thread-safe.</b> Every call comes from a transport thread while the fetch is waiting,
/// so an implementation may touch no <c>Engine</c>, no <c>JsValue</c> and no realm, and two requests may
/// reserve against the same instance at once.
/// </para>
/// <para>
/// <b>Reservations cover backing capacity, not the body's final length.</b> The reader grows a buffer and
/// charges each growth before any byte lands in it, so an implementation is told how much memory is about to
/// exist rather than how much of it turned out to be body.
/// </para>
/// <para>
/// The shape of this interface is a preview and is declared to the compiler as <c>JINT0002</c>; see
/// <see cref="JintDiagnosticIds"/>.
/// </para>
/// </remarks>
[Experimental(JintDiagnosticIds.PreviewDiagnostic)]
public interface IFetchResponseBodyBudget
{
    /// <summary>
    /// Reserves <paramref name="bytes"/> bytes, all or nothing, and answers whether the allowance had room.
    /// </summary>
    /// <param name="bytes">How many bytes are about to be allocated. Never negative.</param>
    /// <param name="lease">
    /// What releases the reservation, or <see langword="null"/> when the implementation needs nothing
    /// released. Disposed exactly once, by Jint, when the bytes are discarded or replayed to the caller.
    /// </param>
    /// <returns><see langword="true"/> when the reservation was granted.</returns>
    /// <remarks>
    /// Refusing is ordinary rather than exceptional: the reader asks for progressively smaller amounts and
    /// treats a refusal of one byte as "the allowance is spent". A <see langword="true"/> answer with a
    /// <see langword="null"/> lease is a grant that costs nothing to release.
    /// </remarks>
    bool TryReserve(int bytes, out IDisposable? lease);
}

/// <summary>
/// The response that ends a chain, plus the one capability that needs the socket: reading its body while the
/// fetch is held at the response stage.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is valid only for the duration of the callback it is handed to.</b> Jint seals it as soon as
/// <see cref="FetchObserver.OnInterceptedResponseAsync"/> returns, and every read afterwards throws: the
/// exchange behind it may already have been disposed, substituted or handed to whoever asked for the
/// resource.
/// </para>
/// <para>
/// <b>No <see cref="Stream"/> crosses this surface.</b> A read either answers immutable bytes the caller may
/// hold for the rest of the callback, or answers <see langword="null"/> because the budget refused them —
/// which is the whole reason the budget is a parameter rather than an ambient setting.
/// </para>
/// <para>
/// <b>Whatever was read is still delivered.</b> Bytes taken off the socket here are replayed ahead of the
/// unread remainder when the response is delivered, so the caller receives every original byte exactly once
/// whether the read succeeded, was refused, or was never made.
/// </para>
/// <para>
/// The shape of this class is a preview and is declared to the compiler as <c>JINT0002</c>; see
/// <see cref="JintDiagnosticIds"/>.
/// </para>
/// </remarks>
[Experimental(JintDiagnosticIds.PreviewDiagnostic)]
public sealed class FetchResponseInterceptionContext
{
    /// <summary>The first growth, and the floor every later one is measured from.</summary>
    private const int InitialChunk = 8 * 1024;

    private readonly HttpResponseMessage _message;
    private List<IDisposable>? _leases;

    /// <summary>
    /// The retained prefix. Always one byte longer than <see cref="_reserved"/>: that last slot is the
    /// lookahead an unknown-length body needs to tell "exactly at the limit" from "over it".
    /// </summary>
    private byte[]? _buffer;

    private int _length;
    private int _reserved;
    private Stream? _stream;
    private ReadOnlyMemory<byte>? _body;
    private bool _refused;
    private bool _sealed;
    private int _reading;

    internal FetchResponseInterceptionContext(ObservedFetchResponse response, HttpResponseMessage message)
    {
        Response = response;
        _message = message;
    }

    /// <summary>Gets the response that ends the chain, exactly as <c>OnResponseAsync</c> is given it.</summary>
    public ObservedFetchResponse Response { get; }

    /// <summary>
    /// Reads the complete response body into memory, charging every byte to <paramref name="budget"/> first.
    /// </summary>
    /// <param name="budget">The allowance the bytes are charged to.</param>
    /// <param name="cancellationToken">Cancelled when the fetch is aborted or times out.</param>
    /// <returns>
    /// The whole body — a non-null empty value when the body was empty — or <see langword="null"/> when
    /// <paramref name="budget"/> refused it.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>A refusal is sticky.</b> Once this has answered <see langword="null"/> for a response, every later
    /// read of that response answers <see langword="null"/> without touching the socket again: the prefix
    /// already taken stays pinned and reading on would spend an allowance that has just said it has none.
    /// </para>
    /// <para>
    /// <b>A success is immutable and reused.</b> The same memory is answered by every later read, and it
    /// stays valid until the callback returns.
    /// </para>
    /// <para>
    /// <b>A failure is a failure.</b> A socket error or a cancellation is thrown rather than becoming a
    /// short body: a partial read must never be mistaken for a complete one. Whatever had been read is still
    /// replayed to the caller.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="budget"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The callback has returned, or another read of this response is already in flight.
    /// </exception>
    public ValueTask<ReadOnlyMemory<byte>?> TryReadBodyAsync(IFetchResponseBodyBudget budget, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(budget);

        if (_sealed)
        {
            throw new InvalidOperationException("The response body can only be read while the response callback is running.");
        }

        if (Interlocked.Exchange(ref _reading, 1) != 0)
        {
            throw new InvalidOperationException("Another read of this response body is already in flight.");
        }

        if (_body is { } cached)
        {
            Volatile.Write(ref _reading, 0);
            return new ValueTask<ReadOnlyMemory<byte>?>(cached);
        }

        if (_refused)
        {
            Volatile.Write(ref _reading, 0);
            return new ValueTask<ReadOnlyMemory<byte>?>((ReadOnlyMemory<byte>?) null);
        }

        return ReadAsync(budget, cancellationToken);
    }

    private async ValueTask<ReadOnlyMemory<byte>?> ReadAsync(IFetchResponseBodyBudget budget, CancellationToken cancellationToken)
    {
        try
        {
            _stream ??= await _message.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            while (true)
            {
                if (_length == _reserved)
                {
                    var desired = _reserved == 0 ? FirstChunk() : Math.Max(_reserved, InitialChunk);
                    if (!Grow(budget, desired))
                    {
                        // The allowance is spent with the buffer exactly full, which is the one state that
                        // cannot say whether the body ended here. One byte of lookahead answers it, into the
                        // slot the buffer always keeps beyond what was reserved.
                        var probe = await _stream.ReadAsync(Lookahead(), cancellationToken).ConfigureAwait(false);
                        if (probe == 0)
                        {
                            return Complete();
                        }

                        _length += probe;
                        _refused = true;
                        return null;
                    }
                }

                var read = await _stream.ReadAsync(_buffer.AsMemory(_length, _reserved - _length), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return Complete();
                }

                _length += read;
            }
        }
        finally
        {
            Volatile.Write(ref _reading, 0);
        }
    }

    /// <summary>How much the first reservation asks for.</summary>
    /// <remarks>
    /// <b><c>Content-Length</c> is a hint that may only make it smaller.</b> A declared length above the
    /// default step is ignored, so a header that lies upwards cannot reserve memory the body will never fill;
    /// one that lies downwards costs another growth step and nothing else. What it buys is that a small body
    /// stops charging a whole step of somebody else's allowance — a twelve-byte response reserving eight
    /// kilobytes is a sibling refused for no reason. The extra byte is the one a body of exactly the declared
    /// length needs to reach its end without a second reservation.
    /// </remarks>
    private int FirstChunk()
    {
        var declared = _message.Content.Headers.ContentLength;
        return declared is >= 0 and < InitialChunk ? (int) declared.Value + 1 : InitialChunk;
    }

    /// <summary>The single slot beyond the reservation, which exists only so that a full buffer can ask.</summary>
    private Memory<byte> Lookahead()
    {
        _buffer ??= new byte[1];
        return _buffer.AsMemory(_length, 1);
    }

    /// <summary>Charges <paramref name="desired"/> bytes, or the largest halving of it the budget grants.</summary>
    private bool Grow(IFetchResponseBodyBudget budget, int desired)
    {
        while (desired > 0)
        {
            if (budget.TryReserve(desired, out var lease))
            {
                if (lease is not null)
                {
                    (_leases ??= []).Add(lease);
                }

                // The extra slot is the lookahead: never handed out, never charged, and written to only on
                // the refusal path, where the byte in it is one the caller must still receive.
                Array.Resize(ref _buffer, _reserved + desired + 1);
                _reserved += desired;
                return true;
            }

            desired /= 2;
        }

        return false;
    }

    private ReadOnlyMemory<byte> Complete()
    {
        var body = _buffer is null ? ReadOnlyMemory<byte>.Empty : new ReadOnlyMemory<byte>(_buffer, 0, _length);
        _body = body;
        return body;
    }

    /// <summary>
    /// Ends the capability and puts every byte read back in front of the still-unread stream, which is what
    /// makes a read invisible to whoever asked for the resource.
    /// </summary>
    internal void AttachReplay()
    {
        _sealed = true;

        if (_length == 0 || _buffer is null || _stream is null)
        {
            // Nothing was taken off the socket, so the content is still exactly what it was. A read that
            // reserved capacity and found an empty body lands here too, and its reservation is released.
            Release();
            return;
        }

        var original = _message.Content;
        var replay = new StreamContent(new FetchReplayStream(
            new ReadOnlyMemory<byte>(_buffer, 0, _length),
            original,
            _stream,
            _leases));

        foreach (var header in original.Headers.NonValidated)
        {
            foreach (var value in header.Value)
            {
                replay.Headers.TryAddWithoutValidation(header.Key, value);
            }
        }

        _message.Content = replay;

        // Ownership of the leases moved to the replay stream, which releases them once the prefix has been
        // handed over or the content is dropped unread.
        _leases = null;
        _buffer = null;
        _body = null;
        _stream = null;
    }

    /// <summary>
    /// Ends the capability for a response nobody will receive — a substitution or a failure — so the bytes
    /// read are released rather than replayed.
    /// </summary>
    internal void Discard()
    {
        _sealed = true;
        Release();
        _buffer = null;
        _body = null;
        _stream = null;
    }

    private void Release()
    {
        if (_leases is not { } leases)
        {
            return;
        }

        _leases = null;
        foreach (var lease in leases)
        {
            lease.Dispose();
        }
    }
}

/// <summary>
/// The prefix a body read took off the socket, in front of the bytes still coming, so the caller receives
/// every original byte exactly once and in order.
/// </summary>
/// <remarks>
/// It owns the original <see cref="HttpContent"/> it displaced: disposing the response disposes the content
/// holding this stream, which disposes both the remainder and the content the remainder came from. The
/// budget leases are released as soon as the prefix has been handed over, because from that moment the only
/// thing holding those bytes is whoever asked for them.
/// </remarks>
internal sealed class FetchReplayStream : Stream
{
    private readonly HttpContent _original;
    private readonly Stream _rest;

    private ReadOnlyMemory<byte> _prefix;
    private List<IDisposable>? _leases;
    private int _position;

    internal FetchReplayStream(ReadOnlyMemory<byte> prefix, HttpContent original, Stream rest, List<IDisposable>? leases)
    {
        _prefix = prefix;
        _original = original;
        _rest = rest;
        _leases = leases;
    }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        if (TryReplay(buffer, out var replayed))
        {
            return replayed;
        }

        return _rest.Read(buffer);
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (TryReplay(buffer.Span, out var replayed))
        {
            return new ValueTask<int>(replayed);
        }

        return _rest.ReadAsync(buffer, cancellationToken);
    }

    /// <summary>
    /// Hands over the next span of the prefix, never mixing it with a byte off the socket: a short read is
    /// what every <see cref="Stream"/> consumer already handles, and one read spanning both would have to
    /// touch the socket to answer.
    /// </summary>
    private bool TryReplay(Span<byte> buffer, out int replayed)
    {
        var remaining = _prefix.Length - _position;
        if (remaining <= 0)
        {
            replayed = 0;
            return false;
        }

        if (buffer.IsEmpty)
        {
            replayed = 0;
            return true;
        }

        var count = Math.Min(remaining, buffer.Length);
        _prefix.Span.Slice(_position, count).CopyTo(buffer);
        _position += count;

        if (_position == _prefix.Length)
        {
            _prefix = default;
            ReleaseLeases();
        }

        replayed = count;
        return true;
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _prefix = default;
            ReleaseLeases();
            _rest.Dispose();
            _original.Dispose();
        }

        base.Dispose(disposing);
    }

    private void ReleaseLeases()
    {
        if (Interlocked.Exchange(ref _leases, null) is not { } leases)
        {
            return;
        }

        foreach (var lease in leases)
        {
            lease.Dispose();
        }
    }
}
#endif
