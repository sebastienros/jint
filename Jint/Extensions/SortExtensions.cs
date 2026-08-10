using System.Collections.Generic;
using System.Linq;

namespace Jint;

internal static class SortExtensions
{
    /// <summary>
    /// Orders a sequence with the supplied comparer, stably, and terminating for any comparer at all.
    /// </summary>
    /// <remarks>
    /// Deliberately not <c>Enumerable.Order</c> or <c>Enumerable.OrderBy</c>, on any target framework. A
    /// JavaScript comparison function is free to be inconsistent -- <c>function (x, y) { return -1; }</c> is
    /// legal, and https://tc39.es/ecma262/#sec-sortcompare leaves the resulting order implementation-defined
    /// while still requiring the sort to finish -- and neither BCL sort finishes. .NET Framework's LINQ sorts
    /// with a plain quicksort that has neither a recursion-depth limit nor a fallback, so it spins forever.
    /// .NET Core's introsort detects the inconsistency and throws <see cref="ArgumentException"/>, which is not
    /// a <c>JavaScriptException</c>, so it escapes the script's own try/catch and the engine entry point.
    ///
    /// A merge sort is stable, always O(n log n), and terminates for any comparer whatsoever, which is the
    /// behaviour the three script-visible sorts need. Being unconditional, it is also what makes them produce
    /// one identical order on every target framework.
    /// </remarks>
    internal static T[] StableOrder<T>(this IEnumerable<T> source, IComparer<T>? comparer)
    {
        // Copy rather than sort in place; the sources here are live views over the array being sorted.
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
}
