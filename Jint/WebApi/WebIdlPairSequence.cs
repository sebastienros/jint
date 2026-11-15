#if NET8_0_OR_GREATER
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Jint.Native;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.WebApi;

/// <summary>
/// One element of a converted <c>sequence&lt;sequence&lt;T&gt;&gt;</c> whose consumer wants pairs: its first two elements
/// and its size, which is all a "size is not 2" check and the append after it read. <see cref="Size"/> saturates at 3.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct WebIdlPair(string? Name, string? Value, int Size);

/// <summary>
/// The <c>sequence&lt;sequence&lt;T&gt;&gt;</c> conversion that <c>HeadersInit</c> and <c>URLSearchParams</c>' init share:
/// https://webidl.spec.whatwg.org/#create-sequence-from-iterable for the outer sequence, and again for each of its
/// elements (https://webidl.spec.whatwg.org/#js-sequence).
/// </summary>
/// <remarks>
/// The conversion runs to completion before its consumer looks at the result. Every inner sequence is read to its end
/// and every element converted, whatever the sequence's length, so a pair the consumer refuses afterwards (its size, a
/// header's name) finds both iterators done and closes nothing. A conversion that fails closes the inner iterator and
/// then the outer one, and a step that throws leaves its own record done and closes nothing (sebastienros/jint#4431).
/// </remarks>
internal static class WebIdlPairSequence
{
    /// <summary>
    /// Converts what <paramref name="outer"/> iterates, converting each element of each inner sequence with
    /// <paramref name="convert"/>; an element that is not an object is a <c>TypeError</c> saying
    /// <paramref name="notASequence"/>.
    /// </summary>
    internal static List<WebIdlPair> Convert(
        Engine engine,
        Realm realm,
        IteratorInstance outer,
        Func<Realm, JsValue, string> convert,
        string notASequence)
    {
        var pairs = new List<WebIdlPair>();
        var steps = 0;
        try
        {
            while (outer.TryIteratorStepValue(out var element))
            {
                // A script-supplied iterator is unbounded, so the engine stays interruptible.
                Step(engine, ref steps);
                pairs.Add(ConvertPair(engine, realm, element, convert, notASequence, ref steps));
            }
        }
        catch
        {
            outer.CloseIfNotDone(CompletionType.Throw);
            throw;
        }

        return pairs;
    }

    private static WebIdlPair ConvertPair(
        Engine engine,
        Realm realm,
        JsValue element,
        Func<Realm, JsValue, string> convert,
        string notASequence,
        ref int steps)
    {
        // Converting to sequence<T> requires an object; a primitive (including a number, which is what WPT's
        // `new URLSearchParams([[1]])` passes) is a TypeError before the size is ever counted.
        if (element is not ObjectInstance)
        {
            Throw.TypeError(realm, notASequence);
        }

        var inner = element.GetIterator(realm);
        string? name = null;
        string? value = null;
        var size = 0;
        try
        {
            while (inner.TryIteratorStepValue(out var item))
            {
                var converted = convert(realm, item);
                if (size == 0)
                {
                    name = converted;
                }
                else if (size == 1)
                {
                    value = converted;
                }

                if (size < 3)
                {
                    size++;
                }

                Step(engine, ref steps);
            }
        }
        catch
        {
            inner.CloseIfNotDone(CompletionType.Throw);
            throw;
        }

        return new WebIdlPair(name, value, size);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Step(Engine engine, ref int steps)
    {
        if (++steps % Engine.ConstraintCheckInterval == 0)
        {
            engine.Constraints.Check();
        }
    }
}
#endif
