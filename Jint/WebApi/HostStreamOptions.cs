#if NET8_0_OR_GREATER
using Jint.Runtime;

namespace Jint.WebApi;

/// <summary>
/// How <c>Engine.Advanced.CreateReadableStream</c> reads a host <see cref="Stream"/>. Requires .NET 8 or
/// higher.
/// </summary>
/// <remarks>
/// An options instance is read once, when the stream is created, and never held: one instance may be reused
/// for any number of streams and any number of engines.
/// </remarks>
public sealed class HostReadableStreamOptions
{
    private int _chunkSize = 64 * 1024;
    private double _highWaterMark = 1;

    /// <summary>
    /// The most bytes one read takes from the host's stream, and therefore the largest <c>Uint8Array</c> a
    /// chunk can be. Defaults to 64 KiB.
    /// </summary>
    /// <remarks>
    /// One buffer of this size is allocated per stream and reused for its whole life, because only one read
    /// is ever in flight. A chunk may be <i>smaller</i> than this whenever the host's stream answers a short
    /// read, which most of them are allowed to do; a script must not assume a fixed chunk size.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public int ChunkSize
    {
        get => _chunkSize;
        set
        {
            if (value <= 0)
            {
                Throw.ArgumentOutOfRangeException(nameof(value), "The chunk size must be positive.");
            }

            _chunkSize = value;
        }
    }

    /// <summary>
    /// How many chunks the stream reads ahead of its consumer. Defaults to 1, which reads one chunk at a
    /// time and is what a script's own <c>new ReadableStream(source)</c> defaults to.
    /// </summary>
    /// <remarks>
    /// This is the standard's high water mark under a chunk-counting queuing strategy —
    /// https://streams.spec.whatwg.org/#qs-api. Raising it trades memory (<see cref="ChunkSize"/> bytes per
    /// chunk buffered) for fewer stalls on a slow source; zero reads nothing at all until a consumer asks,
    /// which is the tightest backpressure the standard can express.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or not a number.</exception>
    public double HighWaterMark
    {
        get => _highWaterMark;
        set
        {
            if (double.IsNaN(value) || value < 0)
            {
                Throw.ArgumentOutOfRangeException(nameof(value), "The high water mark must be a non-negative number.");
            }

            _highWaterMark = value;
        }
    }

    /// <summary>
    /// Whether the host keeps ownership of the stream. Defaults to <see langword="false"/>: the engine
    /// disposes it once the script has read it to the end, cancelled it, or the engine's globals were
    /// restored.
    /// </summary>
    /// <remarks>
    /// Disposing is the useful default because the <i>script</i> decides when it is done reading, and the
    /// host has no other moment at which it could. Set this when the stream is one the host still needs — a
    /// request body it goes on reading, a <see cref="MemoryStream"/> it means to inspect afterwards.
    /// </remarks>
    public bool LeaveOpen { get; set; }
}

/// <summary>
/// How <c>Engine.Advanced.CreateWritableStream</c> writes to a host <see cref="Stream"/>. Requires .NET 8 or
/// higher.
/// </summary>
/// <remarks>
/// An options instance is read once, when the stream is created, and never held: one instance may be reused
/// for any number of streams and any number of engines.
/// </remarks>
public sealed class HostWritableStreamOptions
{
    private double _highWaterMark = 1;

    /// <summary>
    /// How many chunks the stream accepts before <c>writer.ready</c> stops being resolved. Defaults to 1.
    /// </summary>
    /// <remarks>
    /// This is the standard's high water mark under a chunk-counting queuing strategy —
    /// https://streams.spec.whatwg.org/#qs-api — and it is the whole of the backpressure a script feels: a
    /// script that awaits <c>writer.ready</c> before each write never gets further ahead of the host's disk
    /// than this many chunks. A script that does not await it is not blocked, but the queue it builds is the
    /// engine's memory, so raising this raises what one runaway script can cost.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or not a number.</exception>
    public double HighWaterMark
    {
        get => _highWaterMark;
        set
        {
            if (double.IsNaN(value) || value < 0)
            {
                Throw.ArgumentOutOfRangeException(nameof(value), "The high water mark must be a non-negative number.");
            }

            _highWaterMark = value;
        }
    }

    /// <summary>
    /// Whether the host keeps ownership of the stream. Defaults to <see langword="false"/>: the engine
    /// flushes and disposes it when the script closes the writable stream, and disposes it without flushing
    /// when the script aborts one or the engine's globals are restored.
    /// </summary>
    /// <remarks>
    /// With the default, <c>await writer.close()</c> is the script's proof that every byte reached the host's
    /// stream and that the stream is closed — a failure to flush rejects that promise. With this set, the
    /// close still flushes and still reports a failure, but the host is left holding an open stream.
    /// </remarks>
    public bool LeaveOpen { get; set; }
}

/// <summary>
/// How <c>Engine.Advanced.StartReadableStreamCopy</c> writes a script's <c>ReadableStream</c> to a host
/// <see cref="Stream"/>. Requires .NET 8 or higher.
/// </summary>
/// <remarks>
/// An options instance is read once, when the copy is started, and never held.
/// </remarks>
public sealed class HostStreamCopyOptions
{
    /// <summary>
    /// Whether the host keeps ownership of the destination. Defaults to <see langword="false"/>: the copy
    /// flushes and disposes it when the source stream ends, so a completed operation means the file is
    /// written and closed.
    /// </summary>
    /// <remarks>
    /// A copy that fails — the script's stream errored, the destination refused a write, the operation was
    /// cancelled or abandoned — disposes the destination too, without flushing. Set this to keep a
    /// destination the host writes more to afterwards.
    /// </remarks>
    public bool LeaveOpen { get; set; }

    /// <summary>
    /// Whether a copy that ends early leaves the script's stream alone. Defaults to <see langword="false"/>:
    /// a failed or cancelled copy cancels the source, which is what
    /// <c>pipeTo</c>'s own <c>preventCancel</c> defaults to
    /// (https://streams.spec.whatwg.org/#dom-streampipeoptions-preventcancel).
    /// </summary>
    /// <remarks>
    /// Cancelling the source is what lets a host stream behind it — a socket, a subprocess — find out that
    /// nobody is reading any more. Set this when the script means to go on reading the same stream after the
    /// copy stops, which it can, because the copy releases its reader either way.
    /// </remarks>
    public bool PreventCancel { get; set; }
}
#endif
