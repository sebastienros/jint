using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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

public static class Int32Extensions
{
    extension(int)
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        public static bool TryParse(ReadOnlySpan<char> span, NumberStyles style, IFormatProvider provider, out int value)
        {
            return int.TryParse(span.ToString(), style, provider, out value);
        }
#endif

#if NETFRAMEWORK || NETSTANDARD
        public static int Parse(ReadOnlySpan<char> span, IFormatProvider? provider = null)
        {
            return int.Parse(span.ToString(), NumberStyles.Integer, provider);
        }
#endif

#if NETFRAMEWORK || NETSTANDARD2_0
        public static int Parse(ReadOnlySpan<char> span, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
        {
            return int.Parse(span.ToString(), style, provider);
        }
#endif
    }
}

public static class Int64Extensions
{
    extension(long)
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        public static bool TryParse(ReadOnlySpan<char> span, NumberStyles style, IFormatProvider formatProvider, out long value)
        {
            return long.TryParse(span.ToString(), style, formatProvider, out value);
        }
#endif

#if NETFRAMEWORK || NETSTANDARD
        public static long Parse(ReadOnlySpan<char> span, IFormatProvider? provider = null)
        {
            return long.Parse(span.ToString(), NumberStyles.Integer, provider);
        }
#endif

#if NETFRAMEWORK || NETSTANDARD2_0
        public static long Parse(ReadOnlySpan<char> span, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
        {
            return long.Parse(span.ToString(), style, provider);
        }
#endif
    }
}

public static class DoubleExtensions
{
    extension(double)
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        // double.IsFinite arrived in .NET Core 3.0 / netstandard2.1. Backfill it so spec steps
        // phrased as "if x is finite" read the same on every target framework.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
#endif

#if NETFRAMEWORK || NETSTANDARD
        public static double Parse(ReadOnlySpan<char> span, IFormatProvider? provider = null)
        {
            return double.Parse(span.ToString(), NumberStyles.Float | NumberStyles.AllowThousands, provider);
        }
#endif

#if NETFRAMEWORK || NETSTANDARD2_0
        public static double Parse(ReadOnlySpan<char> span, NumberStyles style = NumberStyles.Float | NumberStyles.AllowThousands, IFormatProvider? provider = null)
        {
            return double.Parse(span.ToString(), style, provider);
        }
#endif
    }
}
