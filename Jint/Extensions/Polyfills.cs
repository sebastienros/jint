using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace Jint;

internal static class Polyfills
{
#if !NET8_0_OR_GREATER
    // Enumerable.Order arrived in .NET 7 and is not in netstandard2.1, so net462, netstandard2.0 and
    // netstandard2.1 all need it.
    //
    // It deliberately does NOT delegate to OrderBy. .NET Framework's LINQ sorts with a plain quicksort
    // that has neither a recursion-depth limit nor a fallback, so an inconsistent comparer makes it spin
    // forever rather than terminate. A JavaScript comparison function is free to be inconsistent — the
    // spec leaves the resulting order implementation-defined but still requires the sort to finish — so
    // that is a reachable hang, not a theoretical one. .NET Core's introsort escapes to heapsort and is
    // why the modern targets are fine. A bottom-up merge sort is stable, always O(n log n), and
    // terminates for any comparer whatsoever, which is the behaviour being backfilled.
    internal static IEnumerable<T> Order<T>(this IEnumerable<T> source, IComparer<T>? comparer)
    {
        // Copy rather than sort a caller-visible array in place; Enumerable.Order never mutates its source.
        var items = source.ToArray();
        if (items.Length > 1)
        {
            MergeSort(items, new T[items.Length], 0, items.Length, comparer ?? Comparer<T>.Default);
        }

        return items;
    }

    private static void MergeSort<T>(T[] items, T[] buffer, int start, int end, IComparer<T> comparer)
    {
        if (end - start <= 1)
        {
            return;
        }

        var middle = start + ((end - start) >> 1);
        MergeSort(items, buffer, start, middle, comparer);
        MergeSort(items, buffer, middle, end, comparer);

        if (comparer.Compare(items[middle - 1], items[middle]) <= 0)
        {
            // Already in order, so the merge would only copy the range back onto itself.
            return;
        }

        int left = start, right = middle, index = start;
        while (left < middle && right < end)
        {
            // Taking the left element on a tie is what makes the sort stable.
            buffer[index++] = comparer.Compare(items[left], items[right]) <= 0 ? items[left++] : items[right++];
        }

        while (left < middle)
        {
            buffer[index++] = items[left++];
        }

        while (right < end)
        {
            buffer[index++] = items[right++];
        }

        System.Array.Copy(buffer, start, items, start, end - start);
    }
#endif

#if NETFRAMEWORK || NETSTANDARD2_0
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static bool Contains(this string source, char c) => source.IndexOf(c) != -1;

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static bool StartsWith(this string source, char c) => source.Length > 0 && source[0] == c;

    // The string.Join(char, ...) overloads were added in .NET Core 2.1 / netstandard2.1.
    // Backfill the IEnumerable<string> overload the codebase uses so call sites can pass a char
    // separator uniformly on every target framework, without a per-call-site #if.
    extension(string)
    {
        public static string Join(char separator, IEnumerable<string?> values) => string.Join(separator.ToString(), values);
    }
#endif

#if NETFRAMEWORK
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static bool Contains(this ReadOnlySpan<string> source, string c) => source.IndexOf(c) != -1;
#endif

#if NETFRAMEWORK || NETSTANDARD2_0
    // The span overloads below arrived in .NET Core 2.1 / netstandard2.1. Each forwards to the
    // pointer-based overload that net462 and netstandard2.0 do have, so none of them allocates -- the
    // obvious `Append(value.ToString())` shape would, on paths that exist to avoid exactly that.

    internal static unsafe StringBuilder Append(this StringBuilder builder, ReadOnlySpan<char> value)
    {
        // Pinning an empty span yields a null pointer, and Append(char*, int) rejects null even for a
        // zero count, where the real span overload simply does nothing. Short-circuit so the polyfill
        // cannot differ from it.
        if (value.IsEmpty)
        {
            return builder;
        }

        fixed (char* p = &MemoryMarshal.GetReference(value))
        {
            return builder.Append(p, value.Length);
        }
    }

    internal static unsafe string GetString(this Encoding encoding, ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return string.Empty;
        }

        fixed (byte* p = &MemoryMarshal.GetReference(bytes))
        {
            return encoding.GetString(p, bytes.Length);
        }
    }

    internal static unsafe void Convert(
        this Encoder encoder,
        ReadOnlySpan<char> chars,
        Span<byte> bytes,
        bool flush,
        out int charsUsed,
        out int bytesUsed,
        out bool completed)
    {
        // Encoder.Convert(char*, ...) rejects a null pointer, and pinning an empty span produces one.
        // Route an empty side through a one-element scratch buffer, still passing the real length of 0,
        // so the call behaves as the span overload does rather than throwing.
        Span<char> charScratch = stackalloc char[1];
        Span<byte> byteScratch = stackalloc byte[1];
        var charSource = chars.IsEmpty ? (ReadOnlySpan<char>) charScratch : chars;
        var byteSource = bytes.IsEmpty ? byteScratch : bytes;

        fixed (char* charsPtr = &MemoryMarshal.GetReference(charSource))
        fixed (byte* bytesPtr = &MemoryMarshal.GetReference(byteSource))
        {
            encoder.Convert(charsPtr, chars.Length, bytesPtr, bytes.Length, flush, out charsUsed, out bytesUsed, out completed);
        }
    }
#endif

#if !NET8_0_OR_GREATER
    // string.GetHashCode(ReadOnlySpan<char>, StringComparison) arrived in .NET Core 2.1 but was not
    // exposed by netstandard2.1. The contract that matters here is only that a sliced string hash
    // equal the flat string's within one process, and StringComparer.Ordinal.GetHashCode(s) is
    // s.GetHashCode() on every target framework, so this is exact. It does materialize the slice,
    // which is the cost these targets already paid.
    extension(string)
    {
        public static int GetHashCode(ReadOnlySpan<char> value, StringComparison comparisonType)
        {
            // StringComparer.FromComparison is itself netstandard2.1+, so the mapping is spelled out.
            var s = value.ToString();
            switch (comparisonType)
            {
                case StringComparison.Ordinal: return StringComparer.Ordinal.GetHashCode(s);
                case StringComparison.OrdinalIgnoreCase: return StringComparer.OrdinalIgnoreCase.GetHashCode(s);
                case StringComparison.CurrentCulture: return StringComparer.CurrentCulture.GetHashCode(s);
                case StringComparison.CurrentCultureIgnoreCase: return StringComparer.CurrentCultureIgnoreCase.GetHashCode(s);
                case StringComparison.InvariantCulture: return StringComparer.InvariantCulture.GetHashCode(s);
                case StringComparison.InvariantCultureIgnoreCase: return StringComparer.InvariantCultureIgnoreCase.GetHashCode(s);
                default:
                    Jint.Runtime.Throw.ArgumentException("The string comparison type passed in is currently not supported.", nameof(comparisonType));
                    return 0;
            }
        }
    }
#endif

#if !NET8_0_OR_GREATER
    // The vectorized MemoryExtensions searches below arrived in .NET 8 and are not in netstandard2.1.
    // Each fallback is the character-at-a-time scan the call site used to spell out in its own #else,
    // so behaviour is identical everywhere and only the throughput differs -- which is exactly the
    // trade these call sites were already making, just written once instead of at every site.
    //
    // ContainsAny is the odd one out: MemoryExtensions.IndexOfAny(span, T, T) exists on every target
    // framework, so this one is vectorized downlevel too.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static bool ContainsAny(this ReadOnlySpan<char> span, char value0, char value1)
        => span.IndexOfAny(value0, value1) >= 0;

    internal static int IndexOfAnyInRange(this ReadOnlySpan<char> span, char lowInclusive, char highInclusive)
    {
        for (var i = 0; i < span.Length; i++)
        {
            if ((uint) (span[i] - lowInclusive) <= (uint) (highInclusive - lowInclusive))
            {
                return i;
            }
        }

        return -1;
    }

    internal static int IndexOfAnyExceptInRange(this ReadOnlySpan<char> span, char lowInclusive, char highInclusive)
    {
        for (var i = 0; i < span.Length; i++)
        {
            if ((uint) (span[i] - lowInclusive) > (uint) (highInclusive - lowInclusive))
            {
                return i;
            }
        }

        return -1;
    }

    internal static bool ContainsAnyInRange(this ReadOnlySpan<char> span, char lowInclusive, char highInclusive)
        => span.IndexOfAnyInRange(lowInclusive, highInclusive) >= 0;

    internal static bool ContainsAnyExceptInRange(this ReadOnlySpan<char> span, char lowInclusive, char highInclusive)
        => span.IndexOfAnyExceptInRange(lowInclusive, highInclusive) >= 0;

    internal static int IndexOfAnyExcept(this ReadOnlySpan<char> span, SearchValues<char> values)
    {
        for (var i = 0; i < span.Length; i++)
        {
            if (!values.Contains(span[i]))
            {
                return i;
            }
        }

        return -1;
    }
#endif
}

internal static class MemoryMarshalPolyfills
{
    extension(MemoryMarshal)
    {
#if !NET8_0_OR_GREATER
        // MemoryMarshal.GetArrayDataReference arrived in .NET 5 and is not in netstandard2.1.
        //
        // GetReference over the array's span is the same address by construction. It is not the same
        // cost everywhere: netstandard2.1 gets the runtime's span and so gets the bounds-check and
        // covariance-check elision the real API is used for, while net462 and netstandard2.0 bind
        // System.Memory's slower span and land roughly where the plain indexer did. Correct on all of
        // them, which is what lets the call sites drop their #if.
        //
        // Deliberately not `ref array[0]`: ldelema on a reference-type array performs the array-type
        // check this helper exists to avoid.
        public static ref T GetArrayDataReference<T>(T[] array) => ref MemoryMarshal.GetReference(array.AsSpan());
#endif
    }
}

// One container per receiver type is required, not stylistic. A static extension member lowers into
// its containing class with the receiver type erased from the signature, so int.Parse, long.Parse and
// double.Parse -- identical parameters, differing only in return type -- collide with CS0111 if they
// share one class. Same reason applies to any future static polyfill whose signature is shared across
// receivers.
//
// Each member mirrors its BCL counterpart exactly, defaults included: an extra optional parameter the
// real API does not have would change overload resolution on the frameworks that do have it, which is
// how a polyfill silently stops being a polyfill. The span-taking numeric overloads arrived in two
// waves -- the NumberStyles ones in .NET Core 2.1 / netstandard2.1, the IFormatProvider ones with
// IParsable<T> in .NET 7 -- hence the two different guards.
internal static class Int32Polyfills
{
    extension(int)
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out int result)
        {
            return int.TryParse(s.ToString(), style, provider, out result);
        }

        public static int Parse(ReadOnlySpan<char> s, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
        {
            return int.Parse(s.ToString(), style, provider);
        }
#endif

#if !NET8_0_OR_GREATER
        public static int Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        {
            return int.Parse(s.ToString(), NumberStyles.Integer, provider);
        }
#endif
    }
}

internal static class Int64Polyfills
{
    extension(long)
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out long result)
        {
            return long.TryParse(s.ToString(), style, provider, out result);
        }

        public static long Parse(ReadOnlySpan<char> s, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
        {
            return long.Parse(s.ToString(), style, provider);
        }
#endif

#if !NET8_0_OR_GREATER
        public static long Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        {
            return long.Parse(s.ToString(), NumberStyles.Integer, provider);
        }
#endif
    }
}

internal static class DoublePolyfills
{
    extension(double)
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        // double.IsFinite arrived in .NET Core 3.0 / netstandard2.1. Backfill it so spec steps
        // phrased as "if x is finite" read the same on every target framework.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        public static double Parse(ReadOnlySpan<char> s, NumberStyles style = NumberStyles.Float | NumberStyles.AllowThousands, IFormatProvider? provider = null)
        {
            return double.Parse(s.ToString(), style, provider);
        }

        public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out double result)
        {
            return double.TryParse(s.ToString(), style, provider, out result);
        }
#endif

#if !NET8_0_OR_GREATER
        public static double Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        {
            return double.Parse(s.ToString(), NumberStyles.Float | NumberStyles.AllowThousands, provider);
        }
#endif
    }
}

internal static class BytePolyfills
{
    extension(byte)
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        // byte.Parse(ReadOnlySpan<char>, ...) arrived in .NET Core 2.1 / netstandard2.1.
        public static byte Parse(ReadOnlySpan<char> s, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
        {
            return byte.Parse(s.ToString(), style, provider);
        }
#endif
    }
}

internal static class BigIntegerPolyfills
{
    extension(BigInteger)
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        // BigInteger.TryParse(ReadOnlySpan<char>, ...) arrived in .NET Core 2.1 / netstandard2.1.
        public static bool TryParse(ReadOnlySpan<char> value, NumberStyles style, IFormatProvider? provider, out BigInteger result)
        {
            return BigInteger.TryParse(value.ToString(), style, provider, out result);
        }
#endif
    }
}
