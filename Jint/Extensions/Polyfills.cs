using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;

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
#endif

#if !NET8_0_OR_GREATER
        public static double Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        {
            return double.Parse(s.ToString(), NumberStyles.Float | NumberStyles.AllowThousands, provider);
        }
#endif
    }
}
