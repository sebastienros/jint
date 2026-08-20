#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.TypedArray;
using Jint.Runtime;

namespace Jint.WebApi.Streams;

/// <summary>
/// Which kind of reader a pull-into descriptor was created for, or "<c>none</c>" once that reader has
/// released its lock and the bytes have nowhere to go but the stream's own queue.
/// <para>
/// https://streams.spec.whatwg.org/#pull-into-descriptor-reader-type
/// </para>
/// </summary>
internal enum PullIntoReaderType
{
    Default,
    Byob,
    None,
}

/// <summary>
/// A pull-into descriptor: one pending "fill this buffer" request, whether it came from a BYOB
/// <c>read(view)</c>, from a default <c>read()</c> against a controller with an
/// <c>autoAllocateChunkSize</c>, or from a reader that has since been released.
/// <para>
/// https://streams.spec.whatwg.org/#pull-into-descriptor
/// </para>
/// </summary>
/// <remarks>
/// A mutable class rather than a struct because the algorithms hold on to the head descriptor across
/// several steps and mutate it in place — <c>bytesFilled</c> grows as data arrives, and <c>buffer</c> is
/// replaced every time the buffer is transferred back and forth between the controller and the underlying
/// source.
/// </remarks>
internal sealed class PullIntoDescriptor
{
    /// <summary>https://streams.spec.whatwg.org/#pull-into-descriptor-buffer</summary>
    internal JsArrayBuffer Buffer { get; set; } = null!;

    /// <summary>https://streams.spec.whatwg.org/#pull-into-descriptor-buffer-byte-length</summary>
    internal int BufferByteLength { get; init; }

    /// <summary>https://streams.spec.whatwg.org/#pull-into-descriptor-byte-offset</summary>
    internal int ByteOffset { get; init; }

    /// <summary>https://streams.spec.whatwg.org/#pull-into-descriptor-byte-length</summary>
    internal int ByteLength { get; init; }

    /// <summary>https://streams.spec.whatwg.org/#pull-into-descriptor-bytes-filled</summary>
    internal int BytesFilled { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#pull-into-descriptor-minimum-fill</summary>
    internal int MinimumFill { get; init; }

    /// <summary>https://streams.spec.whatwg.org/#pull-into-descriptor-element-size</summary>
    internal int ElementSize { get; init; }

    /// <summary>
    /// https://streams.spec.whatwg.org/#pull-into-descriptor-view-constructor, as the element type it names
    /// — <see langword="null"/> for <c>%DataView%</c>.
    /// </summary>
    internal TypedArrayElementType? ViewElementType { get; init; }

    /// <summary>https://streams.spec.whatwg.org/#pull-into-descriptor-reader-type</summary>
    internal PullIntoReaderType ReaderType { get; set; }
}

/// <summary>
/// One entry of a byte stream's internal queue.
/// <para>
/// https://streams.spec.whatwg.org/#readable-byte-stream-queue-entry
/// </para>
/// </summary>
/// <remarks>
/// Mutable, because the head entry is consumed a slice at a time as pull-into descriptors are filled from
/// it: the standard adjusts the entry's byte offset and byte length in place rather than re-queuing a
/// remainder.
/// </remarks>
internal sealed class ReadableByteStreamQueueEntry
{
    internal ReadableByteStreamQueueEntry(JsArrayBuffer buffer, int byteOffset, int byteLength)
    {
        Buffer = buffer;
        ByteOffset = byteOffset;
        ByteLength = byteLength;
    }

    /// <summary>https://streams.spec.whatwg.org/#readable-byte-stream-queue-entry-buffer</summary>
    internal JsArrayBuffer Buffer { get; }

    /// <summary>https://streams.spec.whatwg.org/#readable-byte-stream-queue-entry-byte-offset</summary>
    internal int ByteOffset { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#readable-byte-stream-queue-entry-byte-length</summary>
    internal int ByteLength { get; set; }
}

/// <summary>
/// A byte stream's <c>[[queue]]</c> and <c>[[queueTotalSize]]</c>, which the standard keeps as a pair and
/// updates by hand rather than through the queue-with-sizes operations
/// (<see cref="StreamQueue"/>) the other controllers use.
/// <para>
/// https://streams.spec.whatwg.org/#rbs-controller-internal-slots
/// </para>
/// </summary>
/// <remarks>
/// The total is a byte count, so it is exact in a <see cref="long"/> where the default controller's is a
/// <see cref="double"/> a queuing strategy produced. There is no <c>size()</c> callback to run and nothing
/// to range-check: a byte stream's chunk size is its byte length.
/// </remarks>
internal sealed class ReadableByteStreamQueue
{
    private readonly Queue<ReadableByteStreamQueueEntry> _entries = new();

    /// <summary>The specification's <c>[[queueTotalSize]]</c>, in bytes.</summary>
    internal long TotalSize { get; private set; }

    /// <summary>https://streams.spec.whatwg.org/#readable-byte-stream-controller-enqueue-chunk-to-queue</summary>
    internal void Enqueue(JsArrayBuffer buffer, int byteOffset, int byteLength)
    {
        _entries.Enqueue(new ReadableByteStreamQueueEntry(buffer, byteOffset, byteLength));
        TotalSize += byteLength;
    }

    /// <summary>The head entry, which the fill algorithms copy out of before consuming it.</summary>
    internal ReadableByteStreamQueueEntry Peek() => _entries.Peek();

    /// <summary>
    /// Takes the head entry out whole — what
    /// <c>ReadableByteStreamControllerFillReadRequestFromQueue</c> does before handing it to a default
    /// reader's read request.
    /// </summary>
    internal ReadableByteStreamQueueEntry Dequeue()
    {
        var entry = _entries.Dequeue();
        TotalSize -= entry.ByteLength;
        return entry;
    }

    /// <summary>
    /// Consumes <paramref name="count"/> bytes from the head entry, dropping it once nothing is left. The
    /// in-place adjustment of the head is the standard's own, and is why a queue entry is mutable.
    /// </summary>
    internal void ConsumeFromHead(int count)
    {
        var head = _entries.Peek();
        if (head.ByteLength == count)
        {
            _entries.Dequeue();
        }
        else
        {
            head.ByteOffset += count;
            head.ByteLength -= count;
        }

        TotalSize -= count;
    }

    /// <summary>https://streams.spec.whatwg.org/#reset-queue</summary>
    internal void Reset()
    {
        _entries.Clear();
        TotalSize = 0;
    }
}

/// <summary>
/// A <c>ReadableByteStreamController</c> instance — the controller of a stream constructed with
/// <c>type: "bytes"</c>.
/// <para>
/// https://streams.spec.whatwg.org/#rbs-controller-class
/// </para>
/// </summary>
/// <remarks>
/// There is no <c>[[strategySizeAlgorithm]]</c>: a byte stream's queuing strategy may not carry a
/// <c>size()</c> at all (the constructor raises a <c>RangeError</c> for one), because a chunk's size is its
/// byte length by definition.
/// </remarks>
internal sealed class JsReadableByteStreamController : JsReadableStreamController
{
    internal JsReadableByteStreamController(Engine engine, Realm realm) : base(engine, realm)
    {
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readablebytestreamcontroller-queue and
    /// <c>[[queueTotalSize]]</c>, which the standard keeps synchronized as one structure.
    /// </summary>
    internal ReadableByteStreamQueue Queue { get; } = new();

    /// <summary>https://streams.spec.whatwg.org/#readablebytestreamcontroller-pendingpullintos</summary>
    internal Queue<PullIntoDescriptor> PendingPullIntos { get; } = new();

    /// <summary>
    /// https://streams.spec.whatwg.org/#readablebytestreamcontroller-autoallocatechunksize — a positive
    /// integer when the underlying source asked for automatic buffer allocation, <see langword="null"/>
    /// otherwise.
    /// </summary>
    internal ulong? AutoAllocateChunkSize { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#readablebytestreamcontroller-byobrequest</summary>
    internal JsReadableStreamBYOBRequest? ByobRequest { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#readablebytestreamcontroller-started</summary>
    internal bool Started { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#readablebytestreamcontroller-closerequested</summary>
    internal bool CloseRequested { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#readablebytestreamcontroller-pullagain</summary>
    internal bool PullAgain { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#readablebytestreamcontroller-pulling</summary>
    internal bool Pulling { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#readablebytestreamcontroller-strategyhwm</summary>
    internal double StrategyHighWaterMark { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#readablebytestreamcontroller-pullalgorithm</summary>
    internal Func<JsPromise>? PullAlgorithm { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#readablebytestreamcontroller-cancelalgorithm</summary>
    internal Func<JsValue, JsPromise>? CancelAlgorithm { get; set; }

    /// <summary>
    /// https://streams.spec.whatwg.org/#rbs-controller-private-pull
    /// </summary>
    internal override void PullSteps(ReadRequest readRequest)
        => ReadableByteStreamControllerOperations.PullSteps(this, readRequest);

    /// <summary>
    /// https://streams.spec.whatwg.org/#rbs-controller-private-cancel
    /// </summary>
    internal override JsPromise CancelSteps(JsValue reason)
        => ReadableByteStreamControllerOperations.CancelSteps(this, reason);

    /// <summary>
    /// https://streams.spec.whatwg.org/#abstract-opdef-readablebytestreamcontroller-releasesteps — the head
    /// pull-into descriptor is kept, with its reader type downgraded to "none", and the rest are dropped:
    /// the buffer the underlying source is currently filling has to stay valid for it to respond into, but
    /// nothing is waiting for what lands in it any more, so those bytes go to the stream's queue instead.
    /// </summary>
    internal override void ReleaseSteps()
    {
        if (PendingPullIntos.Count == 0)
        {
            return;
        }

        var firstPendingPullInto = PendingPullIntos.Dequeue();
        firstPendingPullInto.ReaderType = PullIntoReaderType.None;

        PendingPullIntos.Clear();
        PendingPullIntos.Enqueue(firstPendingPullInto);
    }
}
#endif
