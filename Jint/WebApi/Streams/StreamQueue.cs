#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
using Jint.Native;
using Jint.Runtime;

namespace Jint.WebApi.Streams;

/// <summary>
/// The "queue-with-sizes" data structure every stream controller keeps: the specification's paired
/// <c>[[queue]]</c> and <c>[[queueTotalSize]]</c> slots, kept synchronized by construction.
/// <para>
/// https://streams.spec.whatwg.org/#queue-with-sizes
/// </para>
/// </summary>
/// <remarks>
/// The running total is deliberately a running total rather than a sum over the queue, exactly as the
/// standard prescribes — including the "if the total goes below zero, set it to zero" step, which exists
/// because floating-point subtraction is not the inverse of floating-point addition.
/// </remarks>
internal sealed class StreamQueue
{
    private readonly Queue<ValueWithSize> _queue = new();

    /// <summary>The specification's <c>[[queueTotalSize]]</c>.</summary>
    internal double TotalSize { get; private set; }

    /// <summary>Whether <c>[[queue]]</c> is empty.</summary>
    internal bool IsEmpty => _queue.Count == 0;

    /// <summary>
    /// https://streams.spec.whatwg.org/#dequeue-value
    /// </summary>
    internal JsValue Dequeue()
    {
        var valueWithSize = _queue.Dequeue();
        TotalSize -= valueWithSize.Size;

        // "If container.[[queueTotalSize]] < 0, set container.[[queueTotalSize]] to 0. (This can occur due
        // to rounding errors.)"
        if (TotalSize < 0)
        {
            TotalSize = 0;
        }

        return valueWithSize.Value;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#enqueue-value-with-size. Raises a <c>RangeError</c> for a size that
    /// is not a non-negative number, or is <c>+∞</c> — the two ways a queuing strategy's <c>size()</c> can
    /// make the running total meaningless.
    /// </summary>
    internal void Enqueue(Realm realm, JsValue value, double size)
    {
        if (!IsNonNegativeNumber(size))
        {
            Throw.RangeError(realm, "The return value of a queuing strategy's size function must be a non-negative, non-NaN number");
        }

        if (double.IsPositiveInfinity(size))
        {
            Throw.RangeError(realm, "The return value of a queuing strategy's size function must be a finite number");
        }

        _queue.Enqueue(new ValueWithSize(value, size));
        TotalSize += size;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#peek-queue-value
    /// </summary>
    internal JsValue Peek() => _queue.Peek().Value;

    /// <summary>
    /// https://streams.spec.whatwg.org/#reset-queue
    /// </summary>
    internal void Reset()
    {
        _queue.Clear();
        TotalSize = 0;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#is-non-negative-number. The specification's version also answers
    /// false for a value that is not a Number at all; the conversion to <c>unrestricted double</c> has
    /// already happened by the time a size reaches this code, so only NaN and the sign are left to check.
    /// </summary>
    internal static bool IsNonNegativeNumber(double value) => !double.IsNaN(value) && value >= 0;

    /// <summary>
    /// https://streams.spec.whatwg.org/#value-with-size
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct ValueWithSize(JsValue Value, double Size);
}
#endif
